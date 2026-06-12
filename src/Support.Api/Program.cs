using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Support.Api.Filters;
using Support.Api.Middleware;
using Support.Application.Interfaces;
using Support.Infrastructure.BackgroundServices;
using Support.Infrastructure.Persistence;
using Support.Infrastructure.Services;
using System.Text;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Structured logging. preserveStaticLogger keeps the global Log.Logger intact so
// multiple hosts (e.g. WebApplicationFactory in integration tests) can coexist.
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    // Centralized log search when a Seq instance is available (docker-compose ships one)
    var seqUrl = context.Configuration["Seq:Url"];
    if (!string.IsNullOrEmpty(seqUrl))
    {
        configuration.WriteTo.Seq(seqUrl);
    }
}, preserveStaticLogger: true);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Services
builder.Services.AddScoped<IReservationProvider, MockReservationProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

// AI services: use Gemini if API key is configured, otherwise fall back to mock
if (!string.IsNullOrEmpty(builder.Configuration["Gemini:ApiKey"]))
{
    builder.Services.AddScoped<IAiCopilotClient, GeminiCopilotClient>();
    builder.Services.AddScoped<IPolicySearchService, GeminiEmbeddingPolicySearchService>();
}
else
{
    builder.Services.AddScoped<IAiCopilotClient, MockAiCopilotClient>();
    builder.Services.AddScoped<IPolicySearchService, PolicySearchService>();
}

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Handlers: auto-register every concrete *Handler class in the Application assembly
var applicationAssembly = typeof(Support.Application.Common.Result).Assembly;
foreach (var handlerType in applicationAssembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Handler")))
{
    builder.Services.AddScoped(handlerType);
}

// Background Services (classification uses the DB-backed outbox; see ClassificationWorker)
builder.Services.AddHostedService<ClassificationWorker>();
builder.Services.AddHostedService<SlaMonitorService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret is not configured. Set 'Jwt:Secret' via user-secrets or environment variable.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Skydesk",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Skydesk",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Rate limiting: strict on auth (brute-force protection), sane global default.
// Disabled via RateLimiting:Enabled=false (e.g. integration tests).
var rateLimitingEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});

// CORS — origins come from configuration, never code: dev origins live in
// appsettings.Development.json, production sets Cors__AllowedOrigins via env.
// (In production the Nginx proxy serves the SPA same-origin, so CORS is rarely
// even exercised.) AllowCredentials is intentionally NOT used: auth travels in
// the Authorization header, not cookies.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Support.Application.Features.Auth.Commands.Login.LoginCommandValidator>();
builder.Services.AddScoped<ValidationFilter>();

// Error handling (RFC 7807)
builder.Services.AddProblemDetails();

// API versioning: existing routes stay as v1 (default when unspecified);
// clients can pin a version via X-Api-Version header or ?api-version=
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"),
        new Asp.Versioning.QueryStringApiVersionReader("api-version"));
}).AddMvc();

// Behind a reverse proxy (Nginx/App Gateway) the client IP arrives in
// X-Forwarded-For — required for the IP-based rate limiter to be accurate.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

// Distributed tracing: HTTP request spans + outgoing HTTP (Gemini) + custom AI
// spans (Skydesk.AI ActivitySource). The OTLP exporter package is
// deliberately NOT referenced — its transitive gRPC dependencies carry open
// advisories; add an exporter when a collector (Jaeger/Tempo) is deployed.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Skydesk.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => ctx.Request.Path != "/health")
        .AddHttpClientInstrumentation()
        .AddSource(Support.Infrastructure.Services.AiDiagnostics.SourceName));

// Controllers
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ValidationFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Skydesk API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await Support.Infrastructure.Persistence.DbSeeder.SeedAsync(context, passwordHasher);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception handler: log the failure and return ProblemDetails
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError
        });
    });
});

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

// HTTPS redirection only outside Development: in dev the Vite proxy targets
// http://localhost:5098, and a 307 redirect to the https port would turn
// proxied same-origin calls into direct cross-origin ones (CORS + self-signed
// cert failures). In production TLS terminates at the reverse proxy anyway.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
