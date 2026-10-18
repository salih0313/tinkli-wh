using System.Security.Claims;
using System.Text.Json;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Audit;

public sealed class AuditService(WrosDbContext db, IHttpContextAccessor accessor)
{
    public Guid? ActorId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? "system-seed";

    // Callers provide explicit safe projections, never request bodies or Identity entities.
    public void Record(string action, string entity, string? entityId = null,
        object? oldValues = null, object? newValues = null, Guid? validatedActor = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = validatedActor ?? ActorId,
            Action = action, Entity = entity, EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CorrelationId = CorrelationId
        });
    }
}
