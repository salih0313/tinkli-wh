using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Purchasing.SupplierCatalog;
namespace Wros.Api.Controllers;
[ApiController, Route("api/v1/suppliers/{supplierId:guid}/catalog"), Authorize(Policy = PermissionCatalog.PurchasingPolicy)]
public sealed class SupplierCatalogController(SupplierCatalogService service) : ControllerBase
{
    [HttpGet] public Task<PageDto<CatalogDto>> Search(Guid supplierId, [Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20, string? search = null, Guid? importId = null) => service.Search(supplierId, page, pageSize, search, importId);
    [HttpPost] public Task<CatalogDto> Create(Guid supplierId, CatalogRequest request) => service.Save(supplierId, null, request);
    [HttpPut("{id:guid}")] public Task<CatalogDto> Update(Guid supplierId, Guid id, CatalogRequest request) => service.Save(supplierId, id, request);
    [HttpGet("template")] public IActionResult Template() => File(XlsxFile.Template(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "wros-supplier-stock-template.xlsx");
    [HttpPost("imports"), EnableRateLimiting("sensitive"), RequestSizeLimit(11534336), RequestFormLimits(MultipartBodyLengthLimit = 11534336)]
    public Task<ImportPreview> Upload(Guid supplierId, IFormFile file) => service.Upload(supplierId, file);
    [HttpGet("imports")] public Task<PageDto<ImportHistoryDto>> History(Guid supplierId, [Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20) => service.History(supplierId, page, pageSize);
    [HttpGet("imports/{id:guid}")] public Task<ImportPreview> Preview(Guid supplierId, Guid id) => service.GetPreview(supplierId, id);
    [HttpPut("imports/{id:guid}/mapping")] public Task<ImportPreview> Map(Guid supplierId, Guid id, MappingRequest request) => service.Map(supplierId, id, request);
    [HttpPost("imports/{id:guid}/confirm"), EnableRateLimiting("sensitive")] public Task<CatalogDto[]> Confirm(Guid supplierId, Guid id, ImportConfirmRequest request) => service.Confirm(supplierId, id, request);
}
