namespace Support.Application.Features.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQuery
{
    public Guid UserId { get; set; }
    public bool UnreadOnly { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetMyNotificationsResult
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public Guid? TicketId { get; set; }
    public DateTime CreatedAt { get; set; }
}
