using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Wros.Api.Controllers;

[ApiController, AllowAnonymous]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet]
    public ActionResult<SystemInfo> Get() => new SystemInfo("WROS", "v1", "Phase 1 — Identity");
}

public sealed record SystemInfo(string Application, string ApiVersion, string Phase);
