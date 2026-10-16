using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class SessionService(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    WrosDbContext db, AuditService audit, IHttpContextAccessor accessor)
{
    private HttpContext Context => accessor.HttpContext!;

    public async Task Login(LoginRequest request)
    {
        var user = await users.FindByNameAsync(request.UserName);
        if (user is null)
        {
            // Comparable password-hash work for unknown names; never log supplied login names.
            _ = users.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyHash.Value, request.Password);
            audit.Record("LoginFailed", "User");
            await db.SaveChangesAsync();
            throw new ApiProblemException(401, "Invalid credentials or account unavailable.");
        }
        var result = user.IsActive
            ? await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            : SignInResult.NotAllowed;
        if (!result.Succeeded)
        {
            audit.Record(result.IsLockedOut ? "LoginLockedOut" : "LoginFailed", "User", user.Id.ToString());
            await db.SaveChangesAsync();
            throw new ApiProblemException(401, "Invalid credentials or account unavailable.");
        }
        audit.Record("LoginSucceeded", "User", user.Id.ToString(), validatedActor: user.Id);
        await db.SaveChangesAsync();
        // Issue the cookie only after security audit persistence succeeds.
        await signIn.SignInAsync(user, new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
    }

    public async Task<SessionDto> Current()
    {
        var user = await users.GetUserAsync(Context.User) ?? throw new ApiProblemException(401, "Sign in required.");
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var grants = roles.Contains(PermissionCatalog.SuperAdmin) ? PermissionCatalog.All : await db.RolePermissions
            .Where(x => db.UserRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == x.RoleId))
            .Select(x => x.PermissionId).Distinct().OrderBy(x => x).ToArrayAsync();
        var auth = await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        return new SessionDto(user.Id, user.UserName!, roles, grants, user.MustChangePassword, auth.Properties?.ExpiresUtc);
    }

    public async Task ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(Context.User) ?? throw new ApiProblemException(401, "Sign in required.");
        await using var tx = await db.Database.BeginTransactionAsync();
        UserAdministration.Ensure(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = user.Id;
        UserAdministration.Ensure(await users.UpdateAsync(user));
        audit.Record("PasswordChanged", "User", user.Id.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        // Password changes invalidate all sessions. Reauthentication is intentional.
        await signIn.SignOutAsync();
    }

    public async Task Logout()
    {
        var user = await users.GetUserAsync(Context.User) ?? throw new ApiProblemException(401, "Sign in required.");
        await using var tx = await db.Database.BeginTransactionAsync();
        UserAdministration.Ensure(await users.UpdateSecurityStampAsync(user));
        audit.Record("Logout", "User", user.Id.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        await signIn.SignOutAsync();
    }

    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<AppUser>()
        .HashPassword(new AppUser(), Guid.NewGuid().ToString("N")));
}
