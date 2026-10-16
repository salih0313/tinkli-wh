using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Audit;

namespace Wros.Api.Shared.Persistence;

public sealed class WrosDbContext(DbContextOptions<WrosDbContext> options) : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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
