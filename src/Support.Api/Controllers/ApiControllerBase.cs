using Microsoft.AspNetCore.Mvc;
using Support.Application.Common;
using System.Security.Claims;

namespace Support.Api.Controllers;

/// <summary>
/// Shared controller plumbing: claim extraction and Result → HTTP mapping.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Returns the authenticated user's id, or null when the token carries no
    /// parseable id — callers must respond 401 instead of proceeding with Guid.Empty.
    /// </summary>
    protected Guid? TryGetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    protected string? GetTokenPnr() => User.FindFirst("pnr")?.Value;

    protected IActionResult ToErrorResult(ErrorType errorType, string? message, List<string>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Title = message ?? "An error occurred.",
            Status = errorType switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status400BadRequest
            }
        };

        if (errors is { Count: > 1 })
        {
            problem.Extensions["errors"] = errors;
        }

        return StatusCode(problem.Status!.Value, problem);
    }

    protected IActionResult ToActionResult(Result result) =>
        result.IsSuccess ? Ok() : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);

    protected IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Data) : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
}
