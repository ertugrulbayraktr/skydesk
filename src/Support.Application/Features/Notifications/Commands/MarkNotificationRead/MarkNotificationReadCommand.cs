namespace Support.Application.Features.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommand
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
}
