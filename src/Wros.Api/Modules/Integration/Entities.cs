using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Inventory;
namespace Wros.Api.Modules.Integration;

public sealed class IntegrationClient
{
    [MaxLength(80)] public string Id { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
public sealed class ProductMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public string ClientId { get; set; } = "";
    [MaxLength(100)] public string ExternalProductId { get; set; } = "";
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
public sealed class AssertionReplay
{
    [MaxLength(80)] public string ClientId { get; set; } = "";
    [MaxLength(64)] public string TokenIdHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class InventorySyncSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; }
    [MaxLength(80)] public string ClientId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int TotalRows { get; set; }
    [MaxLength(64)] public string ContentHash { get; set; } = "";
    public List<InventorySyncSnapshotLine> Lines { get; set; } = [];
}
public sealed class InventorySyncSnapshotLine
{
    public Guid SnapshotId { get; set; }
    public int Ordinal { get; set; }
    public string Payload { get; set; } = "";
}
public sealed class ExternalOrderDeduction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public string ClientId { get; set; } = "";
    [MaxLength(100)] public string EventId { get; set; } = "";
    [MaxLength(100)] public string ExternalOrderId { get; set; } = "";
    [MaxLength(64)] public string RequestHash { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(80)] public string CorrelationId { get; set; } = "";
    public List<ExternalOrderDeductionLine> Lines { get; set; } = [];
}
public sealed class ExternalOrderDeductionLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExternalOrderDeductionId { get; set; }
    [MaxLength(100)] public string ExternalLineId { get; set; } = "";
    [MaxLength(100)] public string ExternalProductId { get; set; } = "";
    public Guid ProductId { get; set; }
    public Guid BalanceId { get; set; }
    public decimal Quantity { get; set; }
}
public static class IntegrationConfiguration
{
    public static void Apply(ModelBuilder m)
    {
        var c = m.Entity<IntegrationClient>();
        c.HasKey(x => x.Id);
        var p = m.Entity<ProductMapping>();
        p.HasKey(x => x.Id);
        p.HasIndex(x => new { x.ClientId, x.ExternalProductId }).IsUnique();
        p.HasIndex(x => new { x.ProductId, x.ClientId });
        p.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        var a = m.Entity<AssertionReplay>();
        a.HasKey(x => new { x.ClientId, x.TokenIdHash });
        a.HasIndex(x => x.ExpiresAt);
        a.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        var s = m.Entity<InventorySyncSnapshot>();
        s.HasKey(x => x.Id);
        s.Property(x => x.Version).UseIdentityColumn();
        s.HasIndex(x => x.Version).IsUnique();
        s.HasIndex(x => new { x.ClientId, x.CreatedAt });
        s.HasIndex(x => x.ExpiresAt);
        s.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        s.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<InventorySyncSnapshotLine>().HasKey(x => new { x.SnapshotId, x.Ordinal });
        var d = m.Entity<ExternalOrderDeduction>();
        d.HasKey(x => x.Id);
        d.HasIndex(x => new { x.ClientId, x.EventId }).IsUnique();
        d.HasIndex(x => new { x.ClientId, x.ExternalOrderId }).IsUnique();
        d.HasIndex(x => x.CompletedAt);
        d.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        d.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ExternalOrderDeductionId).OnDelete(DeleteBehavior.Restrict);
        var l = m.Entity<ExternalOrderDeductionLine>();
        l.HasKey(x => x.Id);
        l.Property(x => x.Quantity).HasPrecision(18, 0);
        l.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        l.HasOne<InventoryBalance>().WithMany().HasForeignKey(x => x.BalanceId).OnDelete(DeleteBehavior.Restrict);
        l.HasIndex(x => new { x.ExternalOrderDeductionId, x.ExternalLineId });
        l.ToTable(t => t.HasCheckConstraint("CK_ExternalDeductionLine_Positive", "[Quantity]>0"));
    }
}
