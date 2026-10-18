using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Identity;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Controllers;

[ApiController, Route("api/v1/suppliers"), Authorize(Policy = PermissionCatalog.MasterReadPolicy)]
public sealed class SuppliersController(MasterDataService service) : ControllerBase
{
    [HttpGet]
    public Task<PageDto<MasterDto>> List([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20,
        string? search = null, bool? active = null, Guid? parentId = null) => service.List<Supplier>(page, pageSize, search, active, parentId);
    [HttpGet("{id:guid}")]
    public Task<MasterDto> Get(Guid id) => service.Get<Supplier>(id);
    [HttpPost, Authorize(Policy = PermissionCatalog.SuppliersPolicy)]
    public async Task<ActionResult<MasterDto>> Create(MasterRequest request) => StatusCode(201, await service.Save<Supplier>(null, request));
    [HttpPut("{id:guid}"), Authorize(Policy = PermissionCatalog.SuppliersPolicy)]
    public Task<MasterDto> Update(Guid id, MasterRequest request) => service.Save<Supplier>(id, request);
}
