using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.AssignTicket;

public class AssignTicketHandler
{
    private readonly IApplicationDbContext _context;

    public AssignTicketHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result.Failure("Ticket not found", ErrorType.NotFound);
        }

        var agent = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.AgentId && u.Role == Role.SupportAgent, cancellationToken);

        if (agent == null)
        {
            return Result.Failure("Agent not found or invalid role", ErrorType.NotFound);
        }

        try
        {
            ticket.Assign(request.AgentId);

            var auditEvent = new TicketAuditEvent(
                ticket.Id,
                ActorType.Agent,
                AuditEventType.Assigned,
                request.AssignedByUserId,
                details: $"Assigned to agent {agent.FullName}");

            _context.TicketAuditEvents.Add(auditEvent);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message, ErrorType.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The ticket was modified by another user. Refresh and try again.", ErrorType.Conflict);
        }
    }
}
