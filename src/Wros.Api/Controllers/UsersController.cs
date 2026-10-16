using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Identity;

namespace Wros.Api.Controllers;

[ApiController, Authorize(Policy = PermissionCatalog.ManageUsersPolicy)]
[Route("api/v1/users")]
public sealed class UsersController(UserAdministration users) : ControllerBase
{
    [HttpGet]
    public Task<PageDto<UserDto>> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, string? search = null)
        => users.List(page, pageSize, search);

    [HttpPost, EnableRateLimiting("sensitive")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request) => StatusCode(201, await users.Create(request));

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request) => users.Update(id, request);

    [HttpPost("{id:guid}/reset-password"), EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request)
    {
        await users.ResetPassword(id, request);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock"), EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Unlock(Guid id, UnlockRequest request)
    {
        await users.Unlock(id, request.ConcurrencyStamp);
        return NoContent();
    }
}

public sealed record UnlockRequest([Required] string ConcurrencyStamp);
