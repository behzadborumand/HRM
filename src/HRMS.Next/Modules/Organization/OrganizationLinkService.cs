using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public static class OrganizationLinkRules
{
    public static bool IsAllowedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && !url.Any(char.IsControl)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed class OrganizationLinkService(OrganizationDbContext db)
{
    public Task<List<OrganizationLink>> ListAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return db.OrganizationLinks.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrganizationLink>> ActiveAsync(ActorScope actor, CancellationToken ct)
    {
        if (!actor.IsActive || actor.CompanyId <= 0) throw new UnauthorizedAccessException();
        var rows = await db.OrganizationLinks.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.Active)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
        return rows.Where(x => OrganizationLinkRules.IsAllowedUrl(x.Url)).ToList();
    }

    public async Task<int> CreateAsync(ActorScope actor, string title, string url, string? description,
        int sortOrder, bool active, CancellationToken ct)
    {
        RequireAdmin(actor);
        (title, url, description) = Validate(title, url, description, sortOrder);
        var link = new OrganizationLink
        {
            CompanyId = actor.CompanyId, Title = title, Url = url, Description = description,
            SortOrder = sortOrder, Active = active
        };
        db.OrganizationLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return link.Id;
    }

    public async Task UpdateAsync(ActorScope actor, int id, string title, string url, string? description,
        int sortOrder, bool active, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        (title, url, description) = Validate(title, url, description, sortOrder);
        var link = await db.OrganizationLinks.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        if (!link.RowVersion.SequenceEqual(expectedVersion)) throw new DbUpdateConcurrencyException("پیوند تغییر کرده است.");
        db.Entry(link).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        link.Title = title;
        link.Url = url;
        link.Description = description;
        link.SortOrder = sortOrder;
        link.Active = active;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ActorScope actor, int id, byte[] expectedVersion, CancellationToken ct)
    {
        RequireAdmin(actor);
        var link = await db.OrganizationLinks.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException();
        if (!link.RowVersion.SequenceEqual(expectedVersion)) throw new DbUpdateConcurrencyException("پیوند تغییر کرده است.");
        db.Entry(link).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        db.OrganizationLinks.Remove(link);
        await db.SaveChangesAsync(ct);
    }

    private static (string Title, string Url, string? Description) Validate(string title, string url,
        string? description, int sortOrder)
    {
        title = title?.Trim() ?? "";
        url = url?.Trim() ?? "";
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (title.Length is < 1 or > 100 || url.Length is < 1 or > 2000 || description?.Length > 300 ||
            sortOrder is < 0 or > 9999 || !OrganizationLinkRules.IsAllowedUrl(url))
            throw new ArgumentException("عنوان، نشانی یا ترتیب پیوند معتبر نیست.");
        return (title, url, description);
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}
