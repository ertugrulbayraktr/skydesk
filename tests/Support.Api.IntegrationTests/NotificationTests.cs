using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

public class NotificationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _agentId;
    private Guid _notificationId;
    private Guid _otherUsersNotificationId;
    private string _agentToken = null!;

    public NotificationTests(WebApplicationFactory<Program> factory)
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

        var agent = new User("notif-agent@test.com", hasher.HashPassword("Agent123!"), "Notif Agent", Role.SupportAgent);
        var otherAgent = new User("other-agent@test.com", hasher.HashPassword("Agent123!"), "Other Agent", Role.SupportAgent);
        context.Users.AddRange(agent, otherAgent);
        await context.SaveChangesAsync();
        _agentId = agent.Id;

        var myNotification = new Notification(agent.Id, "SLA Breach Alert", "Ticket TKT-1 escalated");
        var otherNotification = new Notification(otherAgent.Id, "SLA Breach Alert", "Ticket TKT-2 escalated");
        context.Notifications.AddRange(myNotification, otherNotification);
        await context.SaveChangesAsync();
        _notificationId = myNotification.Id;
        _otherUsersNotificationId = otherNotification.Id;

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "notif-agent@test.com", password = "Agent123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<JsonElement>();
        _agentToken = loginResult.GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Lists_Only_Own_Notifications()
    {
        var response = await _client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var notifications = result.GetProperty("notifications").EnumerateArray().ToList();

        Assert.Single(notifications);
        Assert.Equal(_notificationId.ToString(), notifications[0].GetProperty("id").GetString());
        Assert.Equal(1, result.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task Mark_Read_Updates_Notification()
    {
        var response = await _client.PostAsync($"/api/notifications/{_notificationId}/read", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = await context.Notifications.AsNoTracking()
            .FirstAsync(n => n.Id == _notificationId);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public async Task Cannot_Mark_Another_Users_Notification()
    {
        var response = await _client.PostAsync($"/api/notifications/{_otherUsersNotificationId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
