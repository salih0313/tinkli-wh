using Wros.Api.Modules.Receiving;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Operations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Audit;

namespace Wros.Api.Shared.Persistence;

public sealed class WrosDbContext(DbContextOptions<WrosDbContext> options) : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteLine> DeliveryNoteLines => Set<DeliveryNoteLine>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<DeliveryNoteAllocation> DeliveryNoteAllocations => Set<DeliveryNoteAllocation>();
    public DbSet<LotPlacement> LotPlacements => Set<LotPlacement>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderRevision> PurchaseOrderRevisions => Set<PurchaseOrderRevision>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseArea> WarehouseAreas => Set<WarehouseArea>();
    public DbSet<StorageRack> StorageRacks => Set<StorageRack>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardImmutableHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardImmutableHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void GuardImmutableHistory()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(x => (x.Entity is StockMovement or PurchaseOrderRevision) && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock movements and PO revision snapshots are append-only.");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        MasterDataConfiguration.Apply(modelBuilder);
        PurchasingConfiguration.Apply(modelBuilder);
        ReceivingConfiguration.Apply(modelBuilder);
        modelBuilder.Entity<Permission>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasMaxLength(80); });
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PermissionId);
        });
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasPrecision(3);
            entity.Property(x => x.UpdatedAt).HasPrecision(3);
        });
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.Entity).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.CorrelationId).HasMaxLength(80);
            entity.Property(x => x.Timestamp).HasPrecision(3);
            entity.HasIndex(x => new { x.Timestamp, x.Id });
            entity.HasIndex(x => new { x.ActorId, x.Timestamp });
            entity.HasIndex(x => new { x.Entity, x.EntityId, x.Timestamp });
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WrosDbContext).Assembly);
    }
}
