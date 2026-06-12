using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IReservationProvider _reservationProvider;

    public CreateTicketHandler(
        IApplicationDbContext context,
        IReservationProvider reservationProvider)
    {
        _context = context;
        _reservationProvider = reservationProvider;
    }

    public async Task<Result<CreateTicketResult>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        // The PNR in the request must be the one the passenger authenticated with —
        // otherwise a passenger could open tickets against someone else's booking.
        if (!string.IsNullOrEmpty(request.TokenPnr) &&
            !string.Equals(request.PNR, request.TokenPnr, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateTicketResult>.Failure(
                "You can only create tickets for the reservation you verified.", ErrorType.Forbidden);
        }

        var reservation = await _reservationProvider.GetReservationAsync(request.PNR, request.PassengerLastName, cancellationToken);
        if (reservation == null)
        {
            return Result<CreateTicketResult>.Failure("Invalid PNR or passenger last name");
        }

        // Save immediately with safe defaults; AI classification runs in the background
        var ticket = new Ticket(
            request.Subject,
            request.Description,
            TicketCategory.General,
            Priority.P3,
            request.UserId,
            request.PNR,
            request.PassengerLastName);

        _context.Tickets.Add(ticket);

        var auditEvent = new TicketAuditEvent(
            ticket.Id,
            ActorType.Passenger,
            AuditEventType.Created,
            request.UserId,
            details: $"Ticket created by passenger with PNR {request.PNR}. AI classification queued.");

        _context.TicketAuditEvents.Add(auditEvent);

        // Outbox row in the SAME SaveChanges as the ticket: classification work
        // survives process restarts and is never enqueued for a ticket that
        // failed to persist (atomicity).
        _context.ClassificationOutbox.Add(new ClassificationOutboxItem(
            ticket.Id,
            $"{request.Subject} {request.Description}",
            request.PNR,
            request.PassengerLastName));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateTicketResult>.Success(new CreateTicketResult
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            ClassificationStatus = "Pending"
        });
    }
}
