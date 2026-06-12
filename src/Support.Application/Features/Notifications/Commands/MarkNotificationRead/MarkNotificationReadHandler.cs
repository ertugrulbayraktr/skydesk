using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadHandler
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationReadHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        // Other users' notifications look like they don't exist
        if (notification == null || notification.UserId != request.UserId)
        {
            return Result.Failure("Notification not found", ErrorType.NotFound);
        }

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
