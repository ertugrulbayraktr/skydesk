using Support.Application.Models;

namespace Support.Application.Features.Tickets.Queries.GetDraftReply;

public class GetDraftReplyQuery
{
    public Guid TicketId { get; set; }
}

public class DraftReplyDto
{
    public string DraftText { get; set; } = null!;
    public List<PolicyCitation> Citations { get; set; } = new();
    public List<string> MissingInfoQuestions { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();
}
