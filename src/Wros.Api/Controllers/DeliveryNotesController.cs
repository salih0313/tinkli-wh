using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Receiving;
using Wros.Api.Modules.Purchasing;
namespace Wros.Api.Controllers;
[ApiController, Route("api/v1/delivery-notes"), Authorize(Policy = PermissionCatalog.ReceivingPolicy)]
public sealed class DeliveryNotesController(ReceivingService service, ReceiptQueries queries) : ControllerBase
{
    [HttpGet] public Task<PageDto<ReceiptSummary>> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, string? search = null, string? status = null) => queries.List(page, pageSize, search, status);
    [HttpGet("{id:guid}")] public Task<ReceiptDto> Get(Guid id) => queries.Get(id);
    [HttpGet("purchase-orders")]
    public async Task<PageDto<ReceiptOrderLookup>> PurchaseOrders([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 100)
        => await queries.PurchaseOrders(page, pageSize);
    [HttpGet("placements")] public Task<PageDto<PlacementDto>> Placements([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 100) => queries.Placements(page, pageSize);
    [HttpGet("{id:guid}/movements")] public Task<PageDto<MovementDto>> Movements(Guid id, [Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20) => queries.Movements(id, page, pageSize);
    [HttpPost, RequestSizeLimit(2000000), EnableRateLimiting("sensitive")]
    public async Task<ActionResult<ReceiptDto>> Create(ReceiptRequest request) => StatusCode(201, await service.Save(null, request));
    [HttpPut("{id:guid}"), RequestSizeLimit(2000000)] public Task<ReceiptDto> Update(Guid id, ReceiptRequest request) => service.Save(id, request);
    [HttpPost("{id:guid}/post"), EnableRateLimiting("sensitive")] public Task<ReceiptDto> Post(Guid id, VersionRequest request) => service.Post(id, request);
    [HttpPost("{id:guid}/correct-allocations"), RequestSizeLimit(2000000), EnableRateLimiting("sensitive")]
    public Task<ReceiptDto> Correct(Guid id, CorrectionRequest request) => service.Correct(id, request);
}
[ApiController, Route("api/v1/receipt-stock"), Authorize(Policy = PermissionCatalog.InventoryPolicy)]
public sealed class ReceiptStockController(ReceiptQueries queries) : ControllerBase
{
    [HttpGet] public Task<PageDto<BalanceDto>> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, Guid? deliveryNoteId = null) => queries.Stock(page, pageSize, deliveryNoteId);
}
