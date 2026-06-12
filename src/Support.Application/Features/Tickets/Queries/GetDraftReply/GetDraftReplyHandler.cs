using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetDraftReply;

/// <summary>
/// Builds the full RAG pipeline for the agent copilot: ticket thread context +
/// reservation details + relevant policy chunks → AI-drafted reply.
/// </summary>
public class GetDraftReplyHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IReservationProvider _reservationProvider;
    private readonly IPolicySearchService _policySearchService;
    private readonly IAiCopilotClient _aiCopilot;

    public GetDraftReplyHandler(
        IApplicationDbContext context,
        IReservationProvider reservationProvider,
        IPolicySearchService policySearchService,
        IAiCopilotClient aiCopilot)
    {
        _context = context;
        _reservationProvider = reservationProvider;
        _policySearchService = policySearchService;
        _aiCopilot = aiCopilot;
    }

    public async Task<Result<DraftReplyDto>> Handle(GetDraftReplyQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result<DraftReplyDto>.Failure("Ticket not found", ErrorType.NotFound);
        }

        if (ticket.State == TicketState.Closed || ticket.State == TicketState.Cancelled)
        {
            return Result<DraftReplyDto>.Failure($"Cannot draft a reply for a {ticket.State} ticket", ErrorType.Conflict);
        }

        var authorIds = ticket.Messages.Select(m => m.AuthorUserId).Distinct().ToList();
        var authors = await _context.Users.AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var threadContext = new TicketThreadContext
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Category = ticket.Category,
            Priority = ticket.Priority,
            State = ticket.State,
            PNR = ticket.PNR,
            Messages = ticket.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MessageInfo
                {
                    AuthorName = authors.GetValueOrDefault(m.AuthorUserId, "Unknown"),
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    IsInternal = m.IsInternal
                })
                .ToList()
        };

        var reservation = ticket.PNR != null && ticket.PassengerLastName != null
            ? await _reservationProvider.GetReservationAsync(ticket.PNR, ticket.PassengerLastName, cancellationToken)
            : null;

        // Search policies using the latest passenger message (fall back to subject + description)
        var lastPassengerMessage = threadContext.Messages
            .Where(m => !m.IsInternal)
            .Select(m => m.Content)
            .LastOrDefault();
        var searchQuery = $"{ticket.Subject} {lastPassengerMessage ?? ticket.Description}";

        var policyChunks = await _policySearchService.SearchAsync(searchQuery, topK: 5, cancellationToken);

        var draft = await _aiCopilot.DraftAgentReplyAsync(threadContext, reservation, policyChunks, cancellationToken);

        // Deterministic safety pass on top of the model's own rules
        var internalNotes = threadContext.Messages.Where(m => m.IsInternal).Select(m => m.Content);
        var safetyFlags = Services.DraftSafetyInspector.Inspect(draft.DraftText, internalNotes);

        return Result<DraftReplyDto>.Success(new DraftReplyDto
        {
            DraftText = draft.DraftText,
            Citations = draft.Citations,
            MissingInfoQuestions = draft.MissingInfoQuestions,
            NextActions = draft.NextActions,
            RiskFlags = draft.RiskFlags.Concat(safetyFlags).Distinct().ToList()
        });
    }
}
