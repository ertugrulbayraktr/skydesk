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
/// The AI copilot draft-reply endpoint (RAG pipeline) — uses mock AI services.
/// </summary>
public class DraftReplyEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _ticketId;
    private string _agentToken = null!;

    public DraftReplyEndpointTests(WebApplicationFactory<Program> factory)
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

        var passenger = new User("draft-passenger@test.com", "", "Draft Passenger", Role.Passenger);
        var agent = new User("draft-agent@test.com", hasher.HashPassword("Agent123!"), "Draft Agent", Role.SupportAgent);
        context.Users.AddRange(passenger, agent);

        var ticket = new Ticket(
            "Refund for cancelled flight",
            "My flight was cancelled and I want a refund",
            TicketCategory.Refund,
            Priority.P1,
            passenger.Id,
            "ABC123",
            "Doe");
        context.Tickets.Add(ticket);
        context.TicketMessages.Add(new TicketMessage(ticket.Id, passenger.Id, "Please process my refund", isInternal: false));
        await context.SaveChangesAsync();
        _ticketId = ticket.Id;

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "draft-agent@test.com", password = "Agent123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<JsonElement>();
        _agentToken = loginResult.GetProperty("token").GetString()!;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Agent_Gets_Draft_Reply_With_Content()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        var response = await _client.GetAsync($"/api/agent/tickets/{_ticketId}/draft-reply");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(draft.GetProperty("draftText").GetString()));
    }

    [Fact]
    public async Task Draft_Reply_For_Unknown_Ticket_Returns_404()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        var response = await _client.GetAsync($"/api/agent/tickets/{Guid.NewGuid()}/draft-reply");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Passenger_Cannot_Access_Draft_Reply()
    {
        var verify = await _client.PostAsJsonAsync("/api/auth/passenger/verify-pnr",
            new { pnr = "ABC123", lastName = "Doe" });
        var verifyResult = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var passengerToken = verifyResult.GetProperty("token").GetString()!;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", passengerToken);

        var response = await _client.GetAsync($"/api/agent/tickets/{_ticketId}/draft-reply");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
