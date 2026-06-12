using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Tickets.Queries.GetTicketAudit;

public class GetTicketAuditHandler
{
    private readonly IApplicationDbContext _context;

    public GetTicketAuditHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TicketAuditDto>>> Handle(GetTicketAuditQuery request, CancellationToken cancellationToken)
    {
        var ticketExists = await _context.Tickets
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!ticketExists)
        {
            return Result<List<TicketAuditDto>>.Failure("Ticket not found", ErrorType.NotFound);
        }

        var events = await _context.TicketAuditEvents
            .AsNoTracking()
            .Where(e => e.TicketId == request.TicketId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        var actorIds = events.Where(e => e.ActorId.HasValue)
            .Select(e => e.ActorId!.Value)
            .Distinct()
            .ToList();

        var actors = await _context.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var result = events.Select(e => new TicketAuditDto
        {
            Id = e.Id,
            Timestamp = e.Timestamp,
            ActorType = e.ActorType,
            ActorName = e.ActorId.HasValue ? actors.GetValueOrDefault(e.ActorId.Value, "Unknown") : "System",
            EventType = e.EventType,
            BeforeState = e.BeforeState,
            AfterState = e.AfterState,
            Details = e.Details
        }).ToList();

        return Result<List<TicketAuditDto>>.Success(result);
    }
}
