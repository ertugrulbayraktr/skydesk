namespace Support.Application.Features.Tickets.Commands.SubmitDraftFeedback;

public class SubmitDraftFeedbackCommand
{
    public Guid TicketId { get; set; }
    public bool Accepted { get; set; }
    public string? Comment { get; set; }

    // Set from JWT claims
    public Guid UserId { get; set; }
}
