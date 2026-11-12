using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Access;

public sealed record RoleInput(string Name, string? Description, bool CanReadEmployees, bool CanWriteEmployees, bool CanManageTeam, bool Active);
public sealed record RoleRow(int Id, string Name, string? Description, bool CanReadEmployees, bool CanWriteEmployees, bool CanManageTeam, bool Active, byte[] RowVersion, int UserCount);
public sealed record RolePage(IReadOnlyList<RoleRow> Rows, int Total, int Page, int PageSize);

public sealed class RoleAdministrationService(AccessDbContext db, TimeProvider clock)
{
    public async Task<RolePage> ListAsync(ActorScope actor, int page, CancellationToken ct)
    {
        RequireAdmin(actor);
        var rows = db.Roles.AsNoTracking().Where(r => r.CompanyId == actor.CompanyId);
        var count = await rows.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (count + 49) / 50));
        return new(await rows.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((page - 1) * 50).Take(50).Select(r => new RoleRow(r.Id, r.Name, r.Description, r.CanReadEmployees, r.CanWriteEmployees, r.CanManageTeam, r.Active, r.RowVersion, db.Users.Count(u => u.CompanyId == actor.CompanyId && u.RoleId == r.Id))).ToListAsync(ct), count, page, 50);
    }

    public async Task<IReadOnlyList<RoleRow>> ChoicesAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return await db.Roles.AsNoTracking().Where(r => r.CompanyId == actor.CompanyId).OrderBy(r => r.Name).ThenBy(r => r.Id).Select(r => new RoleRow(r.Id, r.Name, r.Description, r.CanReadEmployees, r.CanWriteEmployees, r.CanManageTeam, r.Active, r.RowVersion, db.Users.Count(u => u.CompanyId == actor.CompanyId && u.RoleId == r.Id))).ToListAsync(ct);
    }

    public async Task<int> SaveAsync(ActorScope actor, int id, RoleInput input, byte[]? expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        var name = input.Name?.Trim() ?? "";
        var description = input.Description?.Trim();
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl) || description?.Length > 500)
            throw new ArgumentException("نام نقش الزامی و حداکثر ۱۰۰ نویسه و توضیح حداکثر ۵۰۰ نویسه است.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = id == 0 ? new AccessRole { CompanyId = actor.CompanyId } :
            await db.Roles.SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == actor.CompanyId, ct)
            ?? throw new KeyNotFoundException("نقش یافت نشد.");
        if (id != 0 && (expectedVersion is null || !role.RowVersion.SequenceEqual(expectedVersion)))
            throw new DbUpdateConcurrencyException("نقش تغییر کرده است؛ صفحه را تازه کنید.");
        if (await db.Roles.AnyAsync(r => r.CompanyId == actor.CompanyId && r.Name == name && r.Id != id, ct))
            throw new ArgumentException("نام نقش تکراری است.");
        var previous = $"Active={role.Active};Read={role.CanReadEmployees};Write={role.CanWriteEmployees};Team={role.CanManageTeam}";
        role.Name = name; role.Description = string.IsNullOrEmpty(description) ? null : description;
        role.Active = input.Active; role.CanReadEmployees = input.CanReadEmployees || input.CanWriteEmployees;
        role.CanWriteEmployees = input.CanWriteEmployees; role.CanManageTeam = input.CanManageTeam;
        if (id == 0) db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        db.RoleAudits.Add(new RoleAudit { CompanyId = actor.CompanyId, RoleId = role.Id, ActorUserId = actor.Id,
            AtUtc = clock.GetUtcNow(), Action = id == 0 ? "Create" : input.Active ? "Update" : "Archive",
            Summary = $"{previous} -> Active={role.Active};Read={role.CanReadEmployees};Write={role.CanWriteEmployees};Team={role.CanManageTeam};Name={name}" });
        if (id != 0)
            await db.Users.Where(u => u.CompanyId == actor.CompanyId && u.RoleId == role.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, u => u.IsActive), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<AppUser>().Where(e => e.Entity.CompanyId == actor.CompanyId && e.Entity.RoleId == role.Id).ToList())
            entry.State = EntityState.Detached;
        return role.Id;
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0) throw new UnauthorizedAccessException();
    }
}