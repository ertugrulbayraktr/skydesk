using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// Policy lifecycle: draft edit, publish, archive — and the RAG implication
/// that archived policies drop out of retrieval.
/// </summary>
public class PolicyLifecycleTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public PolicyLifecycleTests(WebApplicationFactory<Program> factory)
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
        context.Users.Add(new User("policy-admin@test.com", hasher.HashPassword("Admin123!"), "Policy Admin", Role.Admin));
        await context.SaveChangesAsync();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "policy-admin@test.com", password = "Admin123!" });
        var result = await login.Content.ReadFromJsonAsync<JsonElement>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", result.GetProperty("token").GetString());
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> CreateDraftAsync(string title = "Test Policy")
    {
        var response = await _client.PostAsJsonAsync("/api/policies",
            new { title, content = "# Test\n\n## Section A\nBaggage allowance is 23kg for economy class passengers." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("policyId").GetString()!;
    }

    [Fact]
    public async Task Draft_Can_Be_Updated_And_Version_Increments()
    {
        var id = await CreateDraftAsync();

        var update = await _client.PutAsJsonAsync($"/api/policies/{id}",
            new { title = "Updated Title", content = "## Section B\nUpdated content here." });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/policies/{id}");
        Assert.Equal("Updated Title", detail.GetProperty("title").GetString());
        Assert.Equal(2, detail.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Published_Policy_Cannot_Be_Updated_Returns_409()
    {
        var id = await CreateDraftAsync();
        var publish = await _client.PostAsync($"/api/policies/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var update = await _client.PutAsJsonAsync($"/api/policies/{id}",
            new { title = "X", content = "Y" });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Archive_Removes_Policy_From_Retrieval()
    {
        var id = await CreateDraftAsync("Baggage Allowance Policy");
        await _client.PostAsync($"/api/policies/{id}/publish", null);

        // Published → searchable
        using (var scope = _factory.Services.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<IPolicySearchService>();
            var before = await search.SearchAsync("baggage allowance economy");
            Assert.Contains(before, c => c.Content.Contains("23kg"));
        }

        var archive = await _client.PostAsync($"/api/policies/{id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        // Archived → no longer retrieved
        using (var scope = _factory.Services.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<IPolicySearchService>();
            var after = await search.SearchAsync("baggage allowance economy");
            Assert.DoesNotContain(after, c => c.Content.Contains("23kg"));
        }
    }

    [Fact]
    public async Task Archiving_A_Draft_Returns_409()
    {
        var id = await CreateDraftAsync();

        var archive = await _client.PostAsync($"/api/policies/{id}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);
    }
}
