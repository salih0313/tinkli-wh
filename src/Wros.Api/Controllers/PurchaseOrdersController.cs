using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Purchasing;
namespace Wros.Api.Controllers;
[ApiController, Route("api/v1/purchase-orders"), Authorize(Policy = PermissionCatalog.PurchasingReadPolicy)]
public sealed class PurchaseOrdersController(PurchaseOrderService service) : ControllerBase
{
    [HttpGet] public Task<PageDto<OrderDto>> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, string? search = null, string? status = null) => service.List(page, pageSize, search, status);
    [HttpGet("{id:guid}")] public Task<OrderDto> Get(Guid id) => service.Get(id);
    [HttpGet("{id:guid}/revisions")] public Task<RevisionDto[]> History(Guid id) => service.History(id);
    [HttpPost, Authorize(Policy = PermissionCatalog.PurchasingPolicy)] public async Task<ActionResult<OrderDto>> Create(OrderRequest request) => StatusCode(201, await service.Save(null, request));
    [HttpPut("{id:guid}"), Authorize(Policy = PermissionCatalog.PurchasingPolicy)] public Task<OrderDto> Update(Guid id, OrderRequest request) => service.Save(id, request);
    [HttpPost("{id:guid}/prepare-print"), Authorize(Policy = PermissionCatalog.PurchasingPolicy)]
    public async Task<IActionResult> PreparePrint(Guid id, VersionRequest request) { await service.PreparePrint(id, request); return NoContent(); }
    [HttpPost("{id:guid}/send"), Authorize(Policy = PermissionCatalog.PurchasingPolicy)] public Task<OrderDto> Send(Guid id, VersionRequest request) => service.Send(id, request);
}
