using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsHandler
{
    private readonly IApplicationDbContext _context;

    public GetMyNotificationsHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetMyNotificationsResult>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications.AsNoTracking()
            .Where(n => n.UserId == request.UserId);

        var unreadCount = await query.CountAsync(n => !n.IsRead, cancellationToken);

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                TicketId = n.TicketId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<GetMyNotificationsResult>.Success(new GetMyNotificationsResult
        {
            Notifications = notifications,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        });
    }
}
