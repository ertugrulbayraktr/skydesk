using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// Refresh token rotation/revocation and account lockout behaviour.
/// </summary>
public class AuthHardeningTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AuthHardeningTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    public async Task InitializeAsync()
    {
        _factory = TestFactoryHelper.Create(_baseFactory);
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        context.Users.Add(new User("hardening-agent@test.com", hasher.HashPassword("Agent123!"), "Hardening Agent", Role.SupportAgent));
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<JsonElement> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Refresh_Token_Rotation_Invalidates_Old_Token()
    {
        var login = await LoginAsync("hardening-agent@test.com", "Agent123!");
        var refreshToken = login.GetProperty("refreshToken").GetString()!;

        // First refresh succeeds and returns a new pair
        var refresh1 = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh1.StatusCode);
        var rotated = await refresh1.Content.ReadFromJsonAsync<JsonElement>();
        var newRefreshToken = rotated.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(refreshToken, newRefreshToken);

        // Replaying the OLD token must fail (rotation revoked it)
        var refresh2 = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh2.StatusCode);

        // The new token still works
        var refresh3 = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = newRefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh3.StatusCode);
    }

    [Fact]
    public async Task Logout_Revokes_Refresh_Token()
    {
        var login = await LoginAsync("hardening-agent@test.com", "Agent123!");
        var refreshToken = login.GetProperty("refreshToken").GetString()!;

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Repeated_Failed_Logins_Lock_The_Account()
    {
        // 5 wrong passwords trigger the lockout
        for (var i = 0; i < 5; i++)
        {
            var failed = await _client.PostAsJsonAsync("/api/auth/login",
                new { email = "hardening-agent@test.com", password = "WrongPassword!" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Correct password is now rejected while locked out
        var lockedResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "hardening-agent@test.com", password = "Agent123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);

        var body = await lockedResponse.Content.ReadAsStringAsync();
        Assert.Contains("locked", body, StringComparison.OrdinalIgnoreCase);
    }
}
