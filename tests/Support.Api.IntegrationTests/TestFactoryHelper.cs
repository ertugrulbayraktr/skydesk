using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Infrastructure.Persistence;
using Support.Infrastructure.Services;

namespace Support.Api.IntegrationTests;

/// <summary>
/// Builds a test host with an isolated InMemory database and mock AI services.
/// </summary>
public static class TestFactoryHelper
{
    public static WebApplicationFactory<Program> Create(WebApplicationFactory<Program> baseFactory, string? dbName = null)
    {
        var testDbName = dbName ?? $"TestDb_{Guid.NewGuid()}";

        return baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:Secret", "TestSecretKeyThatIsAtLeast32CharactersLongForHS256");
            builder.UseSetting("RateLimiting:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var appDbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                if (appDbDescriptor != null) services.Remove(appDbDescriptor);

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(testDbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiCopilotClient));
                if (aiDescriptor != null) services.Remove(aiDescriptor);
                services.AddScoped<IAiCopilotClient, MockAiCopilotClient>();

                var searchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPolicySearchService));
                if (searchDescriptor != null) services.Remove(searchDescriptor);
                services.AddScoped<IPolicySearchService, PolicySearchService>();
            });
        });
    }
}
