using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Wros.Api.Controllers;

[ApiController, AllowAnonymous]
[Route("api/v1/development/diagnostics")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevelopmentDiagnosticsController : ControllerBase
{
    [HttpGet("error")]
    public IActionResult Error() => throw new InvalidOperationException("Phase 0 manual exception probe.");
}

// Remove the controller entirely outside Development.
public sealed class DevelopmentOnlyConvention(bool isDevelopment) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        if (isDevelopment) return;
        foreach (var controller in application.Controllers
            .Where(c => c.ControllerType == typeof(DevelopmentDiagnosticsController)).ToArray())
            application.Controllers.Remove(controller);
    }
}
