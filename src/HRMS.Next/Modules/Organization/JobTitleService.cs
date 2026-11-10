using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public sealed class JobTitleService(OrganizationDbContext db)
{
    public Task<List<JobTitle>> ListAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return db.JobTitles.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderBy(x => x.Name).Take(500).ToListAsync(ct);
    }

    public Task<List<JobTitleOption>> ActiveOptionsAsync(ActorScope actor, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        return db.JobTitles.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new JobTitleOption(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<int> CreateAsync(ActorScope actor, string name, string? description, CancellationToken ct)
    {
        RequireAdmin(actor);
        (name, description) = Normalize(name, description);
        if (await db.JobTitles.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Name == name, ct))
            throw new ArgumentException("عنوان شغلی با این نام وجود دارد.");
        var title = new JobTitle { CompanyId = actor.CompanyId, Name = name, Description = description };
        db.JobTitles.Add(title);
        await db.SaveChangesAsync(ct);
        return title.Id;
    }

    public async Task UpdateAsync(ActorScope actor, int id, string name, string? description, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        (name, description) = Normalize(name, description);
        var title = await db.JobTitles.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        db.Entry(title).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        if (await db.JobTitles.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Id != id && x.Name == name, ct))
            throw new ArgumentException("عنوان شغلی با این نام وجود دارد.");
        title.Name = name;
        title.Description = description;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(ActorScope actor, int id, bool active, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        var title = await db.JobTitles.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        db.Entry(title).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        title.IsActive = active;
        await db.SaveChangesAsync(ct);
    }

    private static (string Name, string? Description) Normalize(string name, string? description)
    {
        name = name?.Trim() ?? "";
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (name.Length is < 1 or > 200 || description?.Length > 1000)
            throw new ArgumentException("نام یا شرح عنوان شغلی معتبر نیست.");
        return (name, description);
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}

public sealed record JobTitleOption(int Id, string Name);
