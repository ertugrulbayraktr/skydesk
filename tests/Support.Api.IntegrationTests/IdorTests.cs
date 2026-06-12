using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// IDOR regression tests: before the passenger-identity fix every PNR token
/// mapped to Guid.Empty, so all passengers shared one identity and could read
/// each other's tickets.
/// </summary>
public class IdorTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public IdorTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    public Task InitializeAsync()
    {
        _factory = TestFactoryHelper.Create(_baseFactory);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> VerifyPnrAsync(string pnr, string lastName)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/passenger/verify-pnr", new { pnr, lastName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Passenger_Cannot_Read_Another_Passengers_Ticket()
    {
        // Passenger A creates a ticket
        var tokenA = await VerifyPnrAsync("ABC123", "Doe");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new
        {
            subject = "Private refund request",
            description = "Contains personal details",
            pnr = "ABC123",
            passengerLastName = "Doe"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = created.GetProperty("ticketId").GetString()!;

        // Passenger B tries to read it → 404 (existence not leaked)
        var tokenB = await VerifyPnrAsync("XYZ789", "Smith");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var stealResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.NotFound, stealResponse.StatusCode);

        // And cannot post messages into it either
        var messageResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new
        {
            content = "Injecting into someone else's thread"
        });
        Assert.Equal(HttpStatusCode.NotFound, messageResponse.StatusCode);
    }

    [Fact]
    public async Task Passengers_Have_Distinct_Identities_In_MyTickets()
    {
        // Passenger A creates a ticket
        var tokenA = await VerifyPnrAsync("ABC123", "Doe");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new
        {
            subject = "A's ticket",
            description = "Belongs to passenger A",
            pnr = "ABC123",
            passengerLastName = "Doe"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Passenger B's "mine" list must be empty
        var tokenB = await VerifyPnrAsync("XYZ789", "Smith");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var mineResponse = await _client.GetAsync("/api/tickets/mine");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var mine = await mineResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(mine.GetProperty("tickets").EnumerateArray());
    }
}
