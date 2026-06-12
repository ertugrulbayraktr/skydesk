using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Tickets.Queries.GetMyTickets;

public class GetMyTicketsHandler
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetMyTicketsHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Result<GetMyTicketsResult>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsNoTracking()
            .Where(t => t.CreatedByUserId == request.UserId);

        if (request.FilterByState.HasValue)
        {
            query = query.Where(t => t.State == request.FilterByState.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var riskCutoff = now + Domain.Entities.Ticket.SlaRiskWindow;

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TicketSummaryDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Subject = t.Subject,
                State = t.State,
                Priority = t.Priority,
                Category = t.Category,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                SlaRisk = t.FirstResponseAt == null
                    ? t.FirstResponseDueAt <= riskCutoff
                    : t.ResolvedAt == null && t.ResolutionDueAt <= riskCutoff
            })
            .ToListAsync(cancellationToken);

        return Result<GetMyTicketsResult>.Success(new GetMyTicketsResult
        {
            Tickets = tickets,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        });
    }
}
