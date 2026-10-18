using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Shared.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Outbound;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Receiving;
namespace Wros.Api.Controllers;

[ApiController, Route("api/v1/stock-transactions"), Authorize(Policy = "StockOperations")]
public sealed class StockTransactionsController(StockTransactionService service, InventoryQueries inventory, ReceiptQueries receipts, WrosDbContext db) : ControllerBase
{
    [HttpGet("suppliers")]
    public async Task<object> Suppliers([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 100)
    {
        var q = db.Suppliers.AsNoTracking().Where(x => x.IsActive);
        return new
        {
            Items = await q.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(),
            Page = page,
            PageSize = pageSize,
            Total = await q.CountAsync()
        };
    }
    [HttpGet("balances")] public Task<PageDto<InventoryRow>> Balances([FromQuery] InventoryFilter filter) => inventory.List(filter);
    [HttpGet("placements")] public Task<PageDto<PlacementDto>> Placements([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 100) => receipts.Placements(page, pageSize);
    [HttpGet("{kind}")]
    public async Task<object> List(string kind, [Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20)
    {
        await Check(kind);
        return await service.List(kind, page, pageSize);
    }
    [HttpGet("{kind}/{id:guid}")]
    public async Task<object> Get(string kind, Guid id)
    {
        await Check(kind);
        return await service.Get(kind, id);
    }
    [HttpPost("{kind}"), RequestSizeLimit(500000), EnableRateLimiting("sensitive")]
    public async Task<object> Post(string kind, StockTransactionRequest request)
    {
        await Check(kind);
        var t = await service.Post(kind, request);
        return await service.Get(kind, t.Id);
    }
    private async Task Check(string kind)
    {
        StockTransactionService.Kind(kind);
        var permission = PermissionCatalog.StockOperations[kind];
        if (!(await HttpContext.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(User, null, permission)).Succeeded)
        {
            HttpContext.RequestServices.GetRequiredService<Wros.Api.Modules.Audit.AuditService>().Record("AccessDenied", "StockOperation", newValues: new { Kind = kind, Permission = permission });
            await db.SaveChangesAsync();
            throw new Wros.Api.Shared.Errors.ApiProblemException(403, "Permission required for this stock operation.");
        }
    }
}
