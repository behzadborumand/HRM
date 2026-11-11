using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlOrganizationLinksTests
{
    [Fact]
    public async Task LinksAreSafeCompanyScopedAndVisibleToActiveCompanyUsers()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var db = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Links " + Guid.NewGuid().ToString("N") };
        var other = new Company { Name = "Other links " + Guid.NewGuid().ToString("N") };
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();
        try
        {
            var admin = new ActorScope(1, "admin", company.Id, true, true, true, true);
            var employee = admin with { IsAdmin = false, CanReadEmployees = false, CanWriteEmployees = false };
            var foreign = admin with { CompanyId = other.Id };
            var service = new OrganizationLinkService(db);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(admin, "Bad", "javascript:alert(1)", null, 0, true, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(admin, "Bad", "https://user:pass@example.com", null, 0, true, CancellationToken.None));
            var id = await service.CreateAsync(admin, "  Help  ", "https://example.com/help", "Documentation", 2, true, CancellationToken.None);
            Assert.Single(await service.ActiveAsync(employee, CancellationToken.None));
            Assert.Empty(await service.ActiveAsync(foreign, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(foreign, id, "Wrong", "https://example.com", null, 1, true, [], CancellationToken.None));
            var version = (await db.OrganizationLinks.AsNoTracking().SingleAsync(x => x.Id == id)).RowVersion;
            await service.UpdateAsync(admin, id, "Help", "https://example.com/help", null, 1, false, version, CancellationToken.None);
            Assert.Empty(await service.ActiveAsync(employee, CancellationToken.None));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(admin, id, "Help", "https://example.com/help", null, 1, false, version, CancellationToken.None));
            version = (await db.OrganizationLinks.AsNoTracking().SingleAsync(x => x.Id == id)).RowVersion;
            await service.DeleteAsync(admin, id, version, CancellationToken.None);
            Assert.Empty(await service.ListAsync(admin, CancellationToken.None));
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.OrganizationLinks.RemoveRange(await db.OrganizationLinks.Where(x => x.CompanyId == company.Id || x.CompanyId == other.Id).ToListAsync());
            db.Companies.RemoveRange(company, other);
            await db.SaveChangesAsync();
        }
    }
}
