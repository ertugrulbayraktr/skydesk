using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetTicketAudit;

public class GetTicketAuditQuery
{
    public Guid TicketId { get; set; }
}

public class TicketAuditDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public ActorType ActorType { get; set; }
    public string? ActorName { get; set; }
    public AuditEventType EventType { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? Details { get; set; }
}
