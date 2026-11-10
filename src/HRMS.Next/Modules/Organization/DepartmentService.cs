using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public sealed class DepartmentService(OrganizationDbContext db)
{
    public Task<List<Department>> ListAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return db.Departments.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderBy(x => x.Name).Take(500).ToListAsync(ct);
    }

    public async Task<int> CreateAsync(ActorScope actor, string name, int? parentId, CancellationToken ct)
    {
        RequireAdmin(actor);
        name = Normalize(name);
        await ValidateParentAsync(actor.CompanyId, parentId, null, ct);
        if (await db.Departments.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Name == name, ct))
            throw new ArgumentException("واحدی با این نام قبلاً ثبت شده است.");
        var department = new Department { CompanyId = actor.CompanyId, Name = name, ParentId = parentId };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return department.Id;
    }

    public async Task UpdateAsync(ActorScope actor, int id, string name, int? parentId, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        name = Normalize(name);
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        if (!department.RowVersion.SequenceEqual(expectedVersion)) throw new DbUpdateConcurrencyException("واحد سازمانی تغییر کرده است.");
        await ValidateParentAsync(actor.CompanyId, parentId, id, ct);
        if (await db.Departments.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Id != id && x.Name == name, ct))
            throw new ArgumentException("واحدی با این نام قبلاً ثبت شده است.");
        db.Entry(department).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        department.Name = name;
        department.ParentId = parentId;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(ActorScope actor, int id, bool active, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        if (!department.RowVersion.SequenceEqual(expectedVersion)) throw new DbUpdateConcurrencyException("واحد سازمانی تغییر کرده است.");
        if (active) await ValidateParentAsync(actor.CompanyId, department.ParentId, id, ct);
        else if (await db.Departments.AnyAsync(x => x.CompanyId == actor.CompanyId && x.ParentId == id && x.IsActive, ct))
            throw new ArgumentException("ابتدا واحدهای زیرمجموعه را غیرفعال کنید.");
        db.Entry(department).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        department.IsActive = active;
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateParentAsync(int companyId, int? parentId, int? departmentId, CancellationToken ct)
    {
        if (parentId is null) return;
        var rows = await db.Departments.AsNoTracking().Where(x => x.CompanyId == companyId)
            .Select(x => new { x.Id, x.ParentId, x.IsActive }).ToListAsync(ct);
        var byId = rows.ToDictionary(x => x.Id);
        var seen = new HashSet<int>();
        var cursor = parentId;
        while (cursor is { } id)
        {
            if (id == departmentId || !seen.Add(id)) throw new ArgumentException("چرخه در ساختار واحدها مجاز نیست.");
            if (!byId.TryGetValue(id, out var parent) || !parent.IsActive)
                throw new ArgumentException("واحد بالادستی فعال در همین شرکت یافت نشد.");
            cursor = parent.ParentId;
        }
    }

    private static string Normalize(string name)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 1 or > 200) throw new ArgumentException("نام واحد سازمانی معتبر نیست.");
        return name;
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}
