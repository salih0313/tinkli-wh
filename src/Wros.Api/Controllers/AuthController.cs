using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Identity;

namespace Wros.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(SessionService sessions, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        await sessions.Login(request);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public Task<SessionDto> Me() => sessions.Current();

    [Authorize, HttpPost("change-password"), EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await sessions.ChangePassword(request);
        return NoContent();
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await sessions.Logout();
        return NoContent();
    }
}
