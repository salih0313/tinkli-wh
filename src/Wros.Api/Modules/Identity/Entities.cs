using Microsoft.AspNetCore.Identity;

namespace Wros.Api.Modules.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public sealed class AppRole : IdentityRole<Guid> { }

public sealed class Permission
{
    public required string Id { get; set; }
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
    public required string PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
