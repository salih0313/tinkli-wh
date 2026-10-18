using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wros.Api.Shared.Errors;

namespace Wros.Api.Shared.Operations;

public abstract class OperationalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
public abstract class MasterRecord : OperationalRecord
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
public static class OperationRules
{
    public static void CheckVersion(OperationalRecord record, string? version)
    {
        if (version is null || Convert.ToBase64String(record.RowVersion) != version)
            throw new ApiProblemException(409, "This record changed. Refresh before saving again.");
    }
    public static string Required(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ApiProblemException(400, $"{field} is required and must be at most {max} characters.");
        return value.Trim();
    }
    public static string? Optional(string? value, int max, string field)
    {
        if (value?.Length > max) throw new ApiProblemException(400, $"{field} must be at most {max} characters.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    public static void Configure<T>(EntityTypeBuilder<T> entity) where T : OperationalRecord
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.Property(x => x.CreatedAt).HasPrecision(3);
        entity.Property(x => x.UpdatedAt).HasPrecision(3);
    }
}
