using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Inventory;
namespace Wros.Api.Controllers;

[ApiController, Route("api/v1/inventory"), Authorize(Policy = PermissionCatalog.InventoryPolicy)]
public sealed class InventoryController(InventoryQueries queries) : ControllerBase
{
    [HttpGet] public Task<PageDto<InventoryRow>> List([FromQuery] InventoryFilter filter) => queries.List(filter);
    [HttpGet("products")] public Task<object> Products([FromQuery] InventoryFilter filter) => queries.Products(filter);
    [HttpGet("dashboard")] public Task<object> Dashboard() => queries.Dashboard();
    [HttpGet("recommendations")]
    public Task<PageDto<InventoryRow>> Recommendations([FromQuery] InventoryFilter filter)
    {
        filter.Expiry = "sellable";
        if (filter.Sort is not ("fifo" or "fefo"))
            filter.Sort = "fifo";
        return queries.List(filter);
    }
    [HttpGet("movements")] public Task<object> Movements([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, [MaxLength(100)] string? search = null, [MaxLength(32)] string? type = null, Guid? lotId = null) => queries.Ledger(page, pageSize, search, type, lotId);
}
