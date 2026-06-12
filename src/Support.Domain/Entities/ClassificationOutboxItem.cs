using Support.Domain.Common;
using Support.Domain.Enums;

namespace Support.Domain.Entities;

/// <summary>
/// Outbox row for the async AI classification pipeline. Written in the same
/// transaction as the ticket, so a process restart never loses pending work
/// (unlike an in-memory queue).
/// </summary>
public class ClassificationOutboxItem : BaseEntity
{
    public Guid TicketId { get; private set; }
    public string FreeText { get; private set; } = null!;
    public string? PNR { get; private set; }
    public string? PassengerLastName { get; private set; }

    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private ClassificationOutboxItem() { } // EF Core

    public ClassificationOutboxItem(Guid ticketId, string freeText, string? pnr, string? passengerLastName)
    {
        TicketId = ticketId;
        FreeText = freeText;
        PNR = pnr;
        PassengerLastName = passengerLastName;
        Status = OutboxStatus.Pending;
    }

    public void MarkCompleted(DateTime utcNow)
    {
        Status = OutboxStatus.Completed;
        ProcessedAt = utcNow;
        UpdateTimestamp();
    }

    public void RegisterFailure(string error, int maxAttempts, DateTime utcNow)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            ProcessedAt = utcNow;
        }
        UpdateTimestamp();
    }
}
