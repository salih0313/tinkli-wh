using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Identity;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Controllers;

[ApiController, Authorize(Policy = PermissionCatalog.AuditPolicy)]
[Route("api/v1/audit")]
public sealed class AuditController(WrosDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20)
    {
        var query = db.AuditLogs.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.ActorId, x.Timestamp, x.Action, x.Entity, x.EntityId, x.OldValues, x.NewValues, x.CorrelationId }).ToListAsync();
        return Ok(new { items, page, pageSize, total });
    }
}
