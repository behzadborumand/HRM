using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Access;

public sealed class AccessService(AccessDbContext db, IPasswordHasher<AppUser> hasher)
{
    public async Task<ActorScope?> ValidateAsync(string username, string password, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username && x.IsActive, ct);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : ToActor(user);
    }

    public async Task<ActorScope?> GetActiveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return user is null ? null : ToActor(user);
    }

    private static ActorScope ToActor(AppUser user) => new(user.Id, user.Username, user.CompanyId,
        user.IsActive, user.IsAdmin, user.CanReadEmployees, user.CanWriteEmployees);
}

public static class AccessRules
{
    public static bool CanRead(ActorScope? user, int companyId) =>
        user is { IsActive: true } && user.CompanyId == companyId && (user.IsAdmin || user.CanReadEmployees || user.CanWriteEmployees);

    public static bool CanWrite(ActorScope? user, int companyId) =>
        user is { IsActive: true } && user.CompanyId == companyId && (user.IsAdmin || user.CanWriteEmployees);
}
