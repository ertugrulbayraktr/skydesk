using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// Regression suite for the passenger identity flow: PNR verification must
/// provision a real user so ticket ownership, messaging and listing all work.
/// </summary>
public class PassengerWorkflowTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public PassengerWorkflowTests(WebApplicationFactory<Program> factory)
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
    public async Task Passenger_Full_Workflow_VerifyPnr_Create_Message_List_Detail()
    {
        // 1. Verify PNR → token with a real user Guid
        var token = await VerifyPnrAsync("ABC123", "Doe");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Create ticket
        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new
        {
            subject = "My flight was cancelled",
            description = "Flight AA100 was cancelled, I need a refund",
            pnr = "ABC123",
            passengerLastName = "Doe"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = created.GetProperty("ticketId").GetString()!;

        // 3. Add a message (was broken before: passenger had no User row → "User not found")
        var messageResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new
        {
            content = "Any update on my refund?"
        });
        Assert.Equal(HttpStatusCode.Created, messageResponse.StatusCode);

        // 4. Ticket appears in "mine"
        var mineResponse = await _client.GetAsync("/api/tickets/mine");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var mine = await mineResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tickets = mine.GetProperty("tickets").EnumerateArray().ToList();
        Assert.Contains(tickets, t => t.GetProperty("id").GetString() == ticketId);

        // 5. Detail view works and includes the message
        var detailResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messages = detail.GetProperty("messages").EnumerateArray().ToList();
        Assert.Contains(messages, m => m.GetProperty("content").GetString() == "Any update on my refund?");
    }

    [Fact]
    public async Task Passenger_Cannot_Create_Ticket_For_Another_Pnr()
    {
        // Authenticated against ABC123 but tries to file a ticket on XYZ789
        var token = await VerifyPnrAsync("ABC123", "Doe");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/tickets", new
        {
            subject = "Sneaky ticket",
            description = "Trying to use someone else's booking",
            pnr = "XYZ789",
            passengerLastName = "Smith"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPnr_With_Wrong_LastName_Is_Rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/passenger/verify-pnr", new
        {
            pnr = "ABC123",
            lastName = "WrongName"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
