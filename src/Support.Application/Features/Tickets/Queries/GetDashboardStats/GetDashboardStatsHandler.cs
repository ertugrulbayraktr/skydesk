using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetDashboardStats;

public class GetDashboardStatsHandler
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetDashboardStatsHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DashboardStatsDto>> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var riskCutoff = now + Ticket.SlaRiskWindow;

        var tickets = await _context.Tickets.AsNoTracking()
            .Select(t => new
            {
                t.State,
                t.Category,
                t.Priority,
                t.CreatedAt,
                t.FirstResponseAt,
                t.FirstResponseDueAt,
                t.ResolvedAt,
                t.ResolutionDueAt
            })
            .ToListAsync(cancellationToken);

        bool IsOpen(TicketState s) => s != TicketState.Closed && s != TicketState.Cancelled;

        var openTickets = tickets.Where(t => IsOpen(t.State)).ToList();

        var atRisk = openTickets.Count(t =>
            t.FirstResponseAt == null
                ? t.FirstResponseDueAt <= riskCutoff
                : t.ResolvedAt == null && t.ResolutionDueAt <= riskCutoff);

        var slaBreached = await _context.TicketAuditEvents.AsNoTracking()
            .Where(e => e.EventType == AuditEventType.SlaBreached)
            .Select(e => e.TicketId)
            .Distinct()
            .CountAsync(cancellationToken);

        var responded = tickets.Where(t => t.FirstResponseAt.HasValue).ToList();
        double? avgFirstResponse = responded.Count > 0
            ? Math.Round(responded.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes), 1)
            : null;

        var feedbackEvents = await _context.TicketAuditEvents.AsNoTracking()
            .Where(e => e.EventType == AuditEventType.DraftFeedback)
            .Select(e => e.Details)
            .ToListAsync(cancellationToken);

        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => now.Date.AddDays(-(6 - offset)))
            .Select(day => new DailyCountDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Count = tickets.Count(t => t.CreatedAt.Date == day)
            })
            .ToList();

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto
        {
            TotalTickets = tickets.Count,
            OpenTickets = openTickets.Count,
            SlaAtRiskCount = atRisk,
            SlaBreachedCount = slaBreached,
            AvgFirstResponseMinutes = avgFirstResponse,
            DraftFeedbackAccepted = feedbackEvents.Count(d => d != null && d.Contains("accepted")),
            DraftFeedbackRejected = feedbackEvents.Count(d => d != null && d.Contains("rejected")),
            ByState = GroupCounts(tickets.Select(t => t.State.ToString())),
            ByCategory = GroupCounts(tickets.Select(t => t.Category.ToString())),
            ByPriority = GroupCounts(tickets.Select(t => t.Priority.ToString())),
            Last7Days = last7Days
        });
    }

    private static Dictionary<string, int> GroupCounts(IEnumerable<string> keys) =>
        keys.GroupBy(k => k).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());
}
