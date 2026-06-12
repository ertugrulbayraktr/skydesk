using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Tickets.Commands.CreateTicket;
using Support.Application.Features.Tickets.Commands.CreateInternalTicket;
using Support.Application.Features.Tickets.Commands.AddMessage;
using Support.Application.Features.Tickets.Commands.AssignTicket;
using Support.Application.Features.Tickets.Commands.TransitionTicket;
using Support.Application.Features.Tickets.Queries.GetMyTickets;
using Support.Application.Features.Tickets.Queries.GetTicketAudit;
using Support.Application.Features.Tickets.Queries.GetTicketById;

namespace Support.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class TicketsController : ApiControllerBase
{
    private readonly CreateTicketHandler _createTicketHandler;
    private readonly CreateInternalTicketHandler _createInternalTicketHandler;
    private readonly AddMessageHandler _addMessageHandler;
    private readonly AssignTicketHandler _assignTicketHandler;
    private readonly TransitionTicketHandler _transitionTicketHandler;
    private readonly GetMyTicketsHandler _getMyTicketsHandler;
    private readonly GetTicketByIdHandler _getTicketByIdHandler;
    private readonly GetTicketAuditHandler _getTicketAuditHandler;

    public TicketsController(
        CreateTicketHandler createTicketHandler,
        CreateInternalTicketHandler createInternalTicketHandler,
        AddMessageHandler addMessageHandler,
        AssignTicketHandler assignTicketHandler,
        TransitionTicketHandler transitionTicketHandler,
        GetMyTicketsHandler getMyTicketsHandler,
        GetTicketByIdHandler getTicketByIdHandler,
        GetTicketAuditHandler getTicketAuditHandler)
    {
        _getTicketAuditHandler = getTicketAuditHandler;
        _createTicketHandler = createTicketHandler;
        _createInternalTicketHandler = createInternalTicketHandler;
        _addMessageHandler = addMessageHandler;
        _assignTicketHandler = assignTicketHandler;
        _transitionTicketHandler = transitionTicketHandler;
        _getMyTicketsHandler = getMyTicketsHandler;
        _getTicketByIdHandler = getTicketByIdHandler;
    }

    [HttpPost]
    [Authorize(Roles = "Passenger")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketCommand command)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        command.UserId = userId.Value;
        command.TokenPnr = GetTokenPnr();

        var result = await _createTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return CreatedAtAction(nameof(GetTicket), new { id = result.Data!.TicketId }, result.Data);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTickets([FromQuery] GetMyTicketsQuery query)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        query.UserId = userId.Value;
        var result = await _getMyTicketsHandler.Handle(query, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetTicketByIdQuery { TicketId = id, RequestingUserId = userId.Value };
        var result = await _getTicketByIdHandler.Handle(query, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddMessageCommand command)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        command.TicketId = id;
        command.UserId = userId.Value;
        var result = await _addMessageHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return CreatedAtAction(nameof(GetTicket), new { id }, result.Data);
    }

    // ===== AGENT/ADMIN OPERATIONS =====

    /// <summary>
    /// Append-only audit trail for a ticket (Agent/Admin only)
    /// </summary>
    [HttpGet("{id}/audit")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketAudit(Guid id)
    {
        var result = await _getTicketAuditHandler.Handle(
            new GetTicketAuditQuery { TicketId = id }, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create internal ticket (Agent/Admin only, PNR optional)
    /// </summary>
    [HttpPost("internal")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(typeof(CreateInternalTicketResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInternalTicket([FromBody] CreateInternalTicketCommand command)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        // Agent/Admin ID must come from JWT claims, not request body
        command.UserId = userId.Value;

        var result = await _createInternalTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return CreatedAtAction(nameof(GetTicket), new { id = result.Data!.TicketId }, result.Data);
    }

    /// <summary>
    /// Assign ticket to an agent (Agent/Admin only)
    /// </summary>
    [HttpPost("{id}/assign")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] AssignTicketRequest request)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var command = new AssignTicketCommand
        {
            TicketId = id,
            AgentId = request.AgentId,
            AssignedByUserId = userId.Value
        };

        var result = await _assignTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return Ok(new { message = "Ticket assigned successfully" });
    }

    /// <summary>
    /// Transition ticket to a new state (Agent/Admin only)
    /// </summary>
    [HttpPost("{id}/transition")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransitionTicket(Guid id, [FromBody] TransitionTicketRequest request)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var command = new TransitionTicketCommand
        {
            TicketId = id,
            NewState = request.NewState,
            UserId = userId.Value
        };

        var result = await _transitionTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return Ok(new { message = "Ticket state transitioned successfully" });
    }

    /// <summary>
    /// Add internal note to ticket (Agent/Admin only, hidden from passengers)
    /// </summary>
    [HttpPost("{id}/internal-notes")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(typeof(AddMessageResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddInternalNote(Guid id, [FromBody] AddInternalNoteRequest request)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var command = new AddMessageCommand
        {
            TicketId = id,
            Content = request.Content,
            IsInternal = true, // Force internal = true for this endpoint
            UserId = userId.Value
        };

        var result = await _addMessageHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

        return CreatedAtAction(nameof(GetTicket), new { id }, result.Data);
    }
}

// ===== REQUEST DTOs FOR API ENDPOINTS =====

/// <summary>
/// Request model for assigning a ticket to an agent
/// </summary>
public record AssignTicketRequest
{
    public Guid AgentId { get; init; }
}

/// <summary>
/// Request model for transitioning ticket state
/// </summary>
public record TransitionTicketRequest
{
    /// <summary>
    /// Target state for the ticket (0=New, 1=Triaged, 2=Assigned, 3=InProgress, 4=WaitingOnPassenger, 5=Resolved, 6=Closed, 7=Cancelled)
    /// </summary>
    public Support.Domain.Enums.TicketState NewState { get; init; }
}

/// <summary>
/// Request model for adding internal note
/// </summary>
public record AddInternalNoteRequest
{
    public string Content { get; init; } = null!;
}
