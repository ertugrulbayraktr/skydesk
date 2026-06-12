using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Tickets.Commands.SubmitDraftFeedback;
using Support.Application.Features.Tickets.Queries.GetAgentQueue;
using Support.Application.Features.Tickets.Queries.GetDashboardStats;
using Support.Application.Features.Tickets.Queries.GetDraftReply;

namespace Support.Api.Controllers;

/// <summary>
/// Agent-specific operations for ticket management and queue handling
/// </summary>
[Authorize(Roles = "SupportAgent,Admin")]
[Route("api/[controller]")]
public class AgentController : ApiControllerBase
{
    private readonly GetAgentQueueHandler _getAgentQueueHandler;
    private readonly GetDraftReplyHandler _getDraftReplyHandler;
    private readonly SubmitDraftFeedbackHandler _submitDraftFeedbackHandler;
    private readonly GetDashboardStatsHandler _getDashboardStatsHandler;

    public AgentController(
        GetAgentQueueHandler getAgentQueueHandler,
        GetDraftReplyHandler getDraftReplyHandler,
        SubmitDraftFeedbackHandler submitDraftFeedbackHandler,
        GetDashboardStatsHandler getDashboardStatsHandler)
    {
        _getAgentQueueHandler = getAgentQueueHandler;
        _getDraftReplyHandler = getDraftReplyHandler;
        _submitDraftFeedbackHandler = submitDraftFeedbackHandler;
        _getDashboardStatsHandler = getDashboardStatsHandler;
    }

    /// <summary>
    /// Operational dashboard: open/SLA counts, distributions, AI feedback rates,
    /// 7-day ticket volume.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _getDashboardStatsHandler.Handle(new GetDashboardStatsQuery(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get agent ticket queue with filtering and paging
    /// </summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(GetAgentQueueResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQueue([FromQuery] GetAgentQueueQuery query)
    {
        var result = await _getAgentQueueHandler.Handle(query, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get agent's own ticket queue (assigned to current agent only)
    /// </summary>
    [HttpGet("my-queue")]
    [ProducesResponseType(typeof(GetAgentQueueResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueue([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var agentId = TryGetUserId();
        if (agentId == null) return Unauthorized();

        var query = new GetAgentQueueQuery
        {
            FilterByAssignedToMe = agentId.Value,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = "Priority", // Prioritize by urgency
            SortDescending = true
        };

        var result = await _getAgentQueueHandler.Handle(query, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Generate an AI-drafted reply for a ticket (RAG over published policies +
    /// reservation context). The draft is a suggestion — the agent reviews and
    /// sends it manually via the messages endpoint.
    /// </summary>
    [HttpGet("tickets/{id}/draft-reply")]
    [ProducesResponseType(typeof(DraftReplyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDraftReply(Guid id)
    {
        var result = await _getDraftReplyHandler.Handle(
            new GetDraftReplyQuery { TicketId = id }, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Record whether the agent accepted or rejected an AI draft — the feedback
    /// loop for measuring copilot quality over time.
    /// </summary>
    [HttpPost("tickets/{id}/draft-feedback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitDraftFeedback(Guid id, [FromBody] DraftFeedbackRequest request)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var result = await _submitDraftFeedbackHandler.Handle(new SubmitDraftFeedbackCommand
        {
            TicketId = id,
            Accepted = request.Accepted,
            Comment = request.Comment,
            UserId = userId.Value
        }, HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(new { message = "Feedback recorded" })
            : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }
}

/// <summary>
/// Request model for AI draft feedback
/// </summary>
public record DraftFeedbackRequest
{
    public bool Accepted { get; init; }
    public string? Comment { get; init; }
}
