using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlJobTitleCatalogTests
{
    [Fact]
    public async Task CatalogRejectsDuplicatesAndCrossCompanyWritesAndArchivesTitles()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var db = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        var first = new Company { Name = "Title catalog " + Guid.NewGuid().ToString("N") };
        var second = new Company { Name = "Foreign catalog " + Guid.NewGuid().ToString("N") };
        db.Companies.AddRange(first, second);
        await db.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(1, "admin", first.Id, true, true, true, true);
            var foreignActor = actor with { CompanyId = second.Id };
            var service = new JobTitleService(db);
            var id = await service.CreateAsync(actor, "  Analyst  ", "Description", CancellationToken.None);
            Assert.Equal("Analyst", (await service.ListAsync(actor, CancellationToken.None)).Single().Name);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(actor, "Analyst", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(foreignActor, id, "Other", null, [], CancellationToken.None));
            var version = (await db.JobTitles.AsNoTracking().SingleAsync(x => x.Id == id)).RowVersion;
            await service.UpdateAsync(actor, id, "Senior Analyst", "Updated", version, CancellationToken.None);
            version = (await db.JobTitles.AsNoTracking().SingleAsync(x => x.Id == id)).RowVersion;
            await service.SetActiveAsync(actor, id, false, version, CancellationToken.None);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(actor, id, "Senior Analyst", "Updated", version, CancellationToken.None));
            Assert.Empty(await service.ActiveOptionsAsync(actor, CancellationToken.None));
            Assert.Empty(await service.ListAsync(foreignActor, CancellationToken.None));
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.JobTitles.RemoveRange(await db.JobTitles.Where(x => x.CompanyId == first.Id).ToListAsync());
            db.Companies.RemoveRange(first, second);
            await db.SaveChangesAsync();
        }
    }
}
