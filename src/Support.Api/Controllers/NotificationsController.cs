using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Notifications.Commands.MarkNotificationRead;
using Support.Application.Features.Notifications.Queries.GetMyNotifications;

namespace Support.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ApiControllerBase
{
    private readonly GetMyNotificationsHandler _getMyNotificationsHandler;
    private readonly MarkNotificationReadHandler _markNotificationReadHandler;

    public NotificationsController(
        GetMyNotificationsHandler getMyNotificationsHandler,
        MarkNotificationReadHandler markNotificationReadHandler)
    {
        _getMyNotificationsHandler = getMyNotificationsHandler;
        _markNotificationReadHandler = markNotificationReadHandler;
    }

    /// <summary>
    /// List the current user's notifications (newest first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetMyNotificationsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var result = await _getMyNotificationsHandler.Handle(new GetMyNotificationsQuery
        {
            UserId = userId.Value,
            UnreadOnly = unreadOnly,
            PageNumber = pageNumber,
            PageSize = pageSize
        }, HttpContext.RequestAborted);

        return ToActionResult(result);
    }

    /// <summary>
    /// Mark one of the current user's notifications as read. Idempotent.
    /// </summary>
    [HttpPost("{id}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = TryGetUserId();
        if (userId == null) return Unauthorized();

        var result = await _markNotificationReadHandler.Handle(new MarkNotificationReadCommand
        {
            NotificationId = id,
            UserId = userId.Value
        }, HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(new { message = "Notification marked as read" })
            : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }
}
