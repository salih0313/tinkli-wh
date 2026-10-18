using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wros.Api.Modules.Integration;
namespace Wros.Api.Controllers;

[ApiController, Route("api/v1/integration"), EnableRateLimiting("integration")]
public sealed class IntegrationController(IntegrationService service) : ControllerBase
{
    private string Client => User.FindFirst("integration_client")!.Value;
    [HttpPost("snapshots"), Authorize(Policy = "Machine:inventory.snapshot")]
    public Task<object> CreateSnapshot() => service.CreateSnapshot(Client);
    [HttpGet("snapshots/{id:guid}"), Authorize(Policy = "Machine:inventory.snapshot")]
    public Task<object> Snapshot(Guid id, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 100) => service.Snapshot(Client, id, page, pageSize);
    [HttpPost("ready-to-deliver"), Authorize(Policy = "Machine:orders.deduct"), RequestSizeLimit(2000000)]
    public Task<object> Deduct(DeductionRequest request) => service.Deduct(Client, request);
}
[ApiController, Route("api/v1/integration-management"), Authorize(Policy = "integration.manage")]
public sealed class IntegrationManagementController(IntegrationService service) : ControllerBase
{
    [HttpGet] public Task<object> Status([Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20) => service.Status(page, pageSize);
    [HttpPost("clients"), EnableRateLimiting("sensitive")] public Task<object> Client(ClientRequest request) => service.SaveClient(request);
    [HttpGet("mappings")] public Task<object> Mappings([MaxLength(80)] string? clientId = null, [Range(1, 100000)] int page = 1, [Range(1, 100)] int pageSize = 20) => service.Mappings(clientId, page, pageSize);
    [HttpPost("mappings")] public Task<object> CreateMapping(MappingRequest request) => service.SaveMapping(null, request);
    [HttpPut("mappings/{id:guid}")] public Task<object> UpdateMapping(Guid id, MappingRequest request) => service.SaveMapping(id, request);
}
