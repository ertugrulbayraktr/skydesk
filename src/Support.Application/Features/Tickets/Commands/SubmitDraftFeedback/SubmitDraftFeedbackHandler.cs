using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.SubmitDraftFeedback;

/// <summary>
/// Records whether the agent accepted or rejected an AI draft. This is the
/// feedback loop for measuring copilot quality (acceptance rate over time).
/// </summary>
public class SubmitDraftFeedbackHandler
{
    private readonly IApplicationDbContext _context;

    public SubmitDraftFeedbackHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(SubmitDraftFeedbackCommand request, CancellationToken cancellationToken)
    {
        var ticketExists = await _context.Tickets
            .AnyAsync(t => t.Id == request.TicketId, cancellationToken);

        if (!ticketExists)
        {
            return Result.Failure("Ticket not found", ErrorType.NotFound);
        }

        var verdict = request.Accepted ? "accepted" : "rejected";
        var details = $"AI draft {verdict} by agent";
        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            var comment = request.Comment.Length > 200 ? request.Comment[..200] : request.Comment;
            details += $". Comment: {comment}";
        }

        _context.TicketAuditEvents.Add(new TicketAuditEvent(
            request.TicketId,
            ActorType.Agent,
            AuditEventType.DraftFeedback,
            request.UserId,
            details: details));

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
