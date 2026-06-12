using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Policies.Commands.CreatePolicy;
using Support.Application.Features.Policies.Commands.PublishPolicy;
using Support.Application.Features.Policies.Commands.ArchivePolicy;
using Support.Application.Features.Policies.Commands.UpdatePolicy;
using Support.Application.Features.Policies.Queries.GetPolicyById;
using Support.Application.Features.Policies.Queries.ListPolicies;

namespace Support.Api.Controllers;

/// <summary>
/// Policy and knowledge base management (Admin only)
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class PoliciesController : ApiControllerBase
{
    private readonly CreatePolicyHandler _createPolicyHandler;
    private readonly PublishPolicyHandler _publishPolicyHandler;
    private readonly GetPolicyByIdHandler _getPolicyByIdHandler;
    private readonly ListPoliciesHandler _listPoliciesHandler;
    private readonly UpdatePolicyHandler _updatePolicyHandler;
    private readonly ArchivePolicyHandler _archivePolicyHandler;

    public PoliciesController(
        CreatePolicyHandler createPolicyHandler,
        PublishPolicyHandler publishPolicyHandler,
        GetPolicyByIdHandler getPolicyByIdHandler,
        ListPoliciesHandler listPoliciesHandler,
        UpdatePolicyHandler updatePolicyHandler,
        ArchivePolicyHandler archivePolicyHandler)
    {
        _createPolicyHandler = createPolicyHandler;
        _publishPolicyHandler = publishPolicyHandler;
        _getPolicyByIdHandler = getPolicyByIdHandler;
        _listPoliciesHandler = listPoliciesHandler;
        _updatePolicyHandler = updatePolicyHandler;
        _archivePolicyHandler = archivePolicyHandler;
    }

    /// <summary>
    /// Update a draft policy (title + content; version increments). 409 for non-drafts.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] UpdatePolicyCommand command)
    {
        command.PolicyId = id;
        var result = await _updatePolicyHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess
            ? Ok(new { message = "Policy updated" })
            : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }

    /// <summary>
    /// Archive a published policy — its chunks drop out of RAG retrieval. 409 for non-published.
    /// </summary>
    [HttpPost("{id}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchivePolicy(Guid id)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var result = await _archivePolicyHandler.Handle(
            new ArchivePolicyCommand { PolicyId = id, ArchivedByUserId = userId.Value },
            HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(new { message = "Policy archived" })
            : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }

    /// <summary>
    /// List policy documents (newest first)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListPoliciesResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPolicies([FromQuery] ListPoliciesQuery query)
    {
        var result = await _listPoliciesHandler.Handle(query, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create a new policy document (draft state)
    /// </summary>
    /// <param name="command">Policy creation details (title and markdown content)</param>
    /// <returns>Created policy ID</returns>
    /// <response code="201">Policy created successfully</response>
    /// <response code="400">Invalid policy data</response>
    /// <response code="403">User not authorized (must be Admin)</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePolicyResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePolicy([FromBody] CreatePolicyCommand command)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        // Admin ID must come from JWT claims
        command.AuthorId = userId.Value;

        var result = await _createPolicyHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
        }

        return CreatedAtAction(
            nameof(GetPolicy), 
            new { id = result.Data!.PolicyId }, 
            result.Data);
    }

    /// <summary>
    /// Publish a policy document (makes it searchable and chunks it for RAG)
    /// </summary>
    /// <param name="id">Policy document ID</param>
    /// <returns>Success status</returns>
    /// <response code="200">Policy published successfully</response>
    /// <response code="400">Invalid request or policy cannot be published</response>
    /// <response code="403">User not authorized (must be Admin)</response>
    /// <response code="404">Policy not found</response>
    [HttpPost("{id}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishPolicy(Guid id)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var command = new PublishPolicyCommand
        {
            PolicyId = id,
            PublishedByUserId = userId.Value
        };

        var result = await _publishPolicyHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
        }

        return Ok(new { message = "Policy published and indexed successfully" });
    }

    /// <summary>
    /// Get policy by ID
    /// </summary>
    /// <param name="id">Policy ID</param>
    /// <returns>Policy details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PolicyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPolicy(Guid id)
    {
        var query = new GetPolicyByIdQuery { PolicyId = id };
        var result = await _getPolicyByIdHandler.Handle(query, HttpContext.RequestAborted);

        return ToActionResult(result);
    }
}
