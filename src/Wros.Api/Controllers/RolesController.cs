using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wros.Api.Modules.Identity;

namespace Wros.Api.Controllers;

[ApiController, Authorize(Policy = PermissionCatalog.ManageRolesPolicy)]
[Route("api/v1/roles")]
public sealed class RolesController(RoleAdministration roles) : ControllerBase
{
    [HttpGet]
    public Task<RoleDto[]> List() => roles.List();

    [HttpGet("permissions")]
    public ActionResult<string[]> Permissions() => PermissionCatalog.All;

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, RolePermissionsRequest request)
    {
        await roles.SetPermissions(id, request);
        return NoContent();
    }
}
