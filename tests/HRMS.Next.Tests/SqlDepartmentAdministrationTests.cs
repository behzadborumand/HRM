using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlDepartmentAdministrationTests
{
    [Fact]
    public async Task HierarchyIsCompanyScopedAcyclicAndArchivedDepartmentsCannotBeAssigned()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var db = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        var first = new Company { Name = "Department admin " + Guid.NewGuid().ToString("N") };
        var second = new Company { Name = "Other department " + Guid.NewGuid().ToString("N") };
        db.Companies.AddRange(first, second);
        await db.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(1, "admin", first.Id, true, true, true, true);
            var foreign = actor with { CompanyId = second.Id };
            var service = new DepartmentService(db);
            var foreignId = await service.CreateAsync(foreign, "Foreign", null, CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(actor, "Invalid", foreignId, CancellationToken.None));
            var root = await service.CreateAsync(actor, "Root", null, CancellationToken.None);
            var child = await service.CreateAsync(actor, "Child", root, CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(actor, "Root", null, CancellationToken.None));
            var rootVersion = (await db.Departments.AsNoTracking().SingleAsync(x => x.Id == root)).RowVersion;
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(actor, root, "Root", child, rootVersion, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(foreign, root, "Wrong", null, rootVersion, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SetActiveAsync(actor, root, false, rootVersion, CancellationToken.None));
            var childVersion = (await db.Departments.AsNoTracking().SingleAsync(x => x.Id == child)).RowVersion;
            await service.SetActiveAsync(actor, child, false, childVersion, CancellationToken.None);
            Assert.False(await new OrganizationDirectory(db).DepartmentBelongsAsync(child, first.Id, CancellationToken.None));
            Assert.Contains(await new OrganizationDirectory(db).DepartmentsAsync(first.Id, CancellationToken.None), x => x.Id == child);
            await service.SetActiveAsync(actor, root, false, rootVersion, CancellationToken.None);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(actor, root, "Root", null, rootVersion, CancellationToken.None));
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.Departments.RemoveRange(await db.Departments.Where(x => (x.CompanyId == first.Id || x.CompanyId == second.Id) && x.ParentId != null).ToListAsync());
            await db.SaveChangesAsync();
            db.Departments.RemoveRange(await db.Departments.Where(x => (x.CompanyId == first.Id || x.CompanyId == second.Id) && x.ParentId == null).ToListAsync());
            db.Companies.RemoveRange(first, second);
            await db.SaveChangesAsync();
        }
    }
}
