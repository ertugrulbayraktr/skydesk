using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Support.Application.Features.Auth.Commands.Login;
using Support.Application.Features.Auth.Commands.Logout;
using Support.Application.Features.Auth.Commands.RefreshToken;
using Support.Application.Features.Auth.Commands.VerifyPnr;

namespace Support.Api.Controllers;

[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly LoginHandler _loginHandler;
    private readonly VerifyPnrHandler _verifyPnrHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;

    public AuthController(
        LoginHandler loginHandler,
        VerifyPnrHandler verifyPnrHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler)
    {
        _loginHandler = loginHandler;
        _verifyPnrHandler = verifyPnrHandler;
        _refreshTokenHandler = refreshTokenHandler;
        _logoutHandler = logoutHandler;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _loginHandler.Handle(command, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpPost("passenger/verify-pnr")]
    public async Task<IActionResult> VerifyPnr([FromBody] VerifyPnrCommand command)
    {
        var result = await _verifyPnrHandler.Handle(command, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Exchange a valid refresh token for a new access + refresh token pair (rotation).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await _refreshTokenHandler.Handle(command, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    /// <summary>
    /// Revoke a refresh token (logout). Idempotent.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        var result = await _logoutHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess
            ? Ok(new { message = "Logged out" })
            : ToErrorResult(result.ErrorType, result.ErrorMessage, result.Errors);
    }
}
