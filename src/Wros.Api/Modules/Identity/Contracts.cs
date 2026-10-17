using System.ComponentModel.DataAnnotations;

namespace Wros.Api.Modules.Identity;

public sealed record LoginRequest([Required, MaxLength(256)] string UserName, [Required] string Password);
public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);
public sealed record CreateUserRequest([Required, RegularExpression(@"[a-zA-Z0-9._@+-]{3,64}")] string UserName,
    [Required] string Password, [Required, MinLength(1), MaxLength(6)] string[] Roles);
public sealed record UpdateUserRequest(bool IsActive, [Required, MinLength(1), MaxLength(6)] string[] Roles,
    [Required] string ConcurrencyStamp);
public sealed record ResetPasswordRequest([Required] string NewPassword, [Required] string ConcurrencyStamp);
public sealed record RolePermissionsRequest([Required, MaxLength(12)] string[] Permissions, [Required] string ConcurrencyStamp);
public sealed record UserDto(Guid Id, string UserName, bool IsActive, bool MustChangePassword, bool IsLockedOut,
    DateTimeOffset? LockoutEnd, string[] Roles, string ConcurrencyStamp, DateTimeOffset CreatedAt);
public sealed record SessionDto(Guid Id, string UserName, string[] Roles, string[] Permissions, bool MustChangePassword, DateTimeOffset? ExpiresAt);
public sealed record RoleDto(Guid Id, string Name, string[] Permissions, string ConcurrencyStamp, bool IsSystemAdmin);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
