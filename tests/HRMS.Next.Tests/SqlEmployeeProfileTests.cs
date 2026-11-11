using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlEmployeeProfileTests
{
    [Fact]
    public async Task ProfileNavigationTeamsAndPhotosStayInCompanyAndPhotoChangesAreAudited()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the disposable trial database is allowed.");
        await using var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var db = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Profile " + Guid.NewGuid().ToString("N") };
        var foreign = new Company { Name = "Foreign profile " + Guid.NewGuid().ToString("N") };
        org.Companies.AddRange(company, foreign);
        await org.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "Profile unit" };
        var otherDepartment = new Department { CompanyId = foreign.Id, Name = "Other" };
        org.Departments.AddRange(department, otherDepartment);
        await org.SaveChangesAsync();
        Employee Person(int companyId, int departmentId, string lastName) => new() {
            CompanyId = companyId, DepartmentId = departmentId, EmployeeNumber = Guid.NewGuid().ToString("N"),
            FirstName = "Same", LastName = lastName, HireDate = new DateOnly(2024, 1, 1)
        };
        var first = Person(company.Id, department.Id, "Alpha");
        var second = Person(company.Id, department.Id, "Alpha");
        var third = Person(company.Id, department.Id, "Zulu");
        var outsider = Person(foreign.Id, otherDepartment.Id, "Alpha");
        db.Employees.AddRange(first, second, third, outsider);
        await db.SaveChangesAsync();
        db.EmployeeJobs.Add(new EmployeeJob { CompanyId = company.Id, EmployeeId = second.Id,
            DepartmentId = department.Id, ManagerEmployeeId = first.Id, JobTitle = "Analyst",
            StartDate = second.HireDate, IsCurrent = true, EmploymentType = "FullTime", PayType = "Salary", PayRate = 750000000m });
        await db.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(81, "profile", company.Id, true, true, true, true);
            var directory = new EmployeeProfileDirectory(db);
            var navigation = await directory.NavigationAsync(actor, second.Id, CancellationToken.None);
            Assert.NotNull(navigation);
            Assert.Equal(3, navigation.Total);
            Assert.Equal(2, navigation.Position);
            Assert.Equal(first.Id, navigation.PreviousId);
            Assert.Equal(third.Id, navigation.NextId);
            Assert.Null(await directory.NavigationAsync(actor, outsider.Id, CancellationToken.None));
            Assert.Null((await directory.NavigationAsync(actor, first.Id, CancellationToken.None))!.PreviousId);
            var team = await directory.DirectReportsAsync(actor, first.Id, CancellationToken.None);
            Assert.Single(team.Rows);
            Assert.Equal(second.Id, team.Rows[0].Id);
            Assert.Empty((await directory.DirectReportsAsync(actor, outsider.Id, CancellationToken.None)).Rows);
            var pay = new EmployeePayService(db);
            Assert.Equal(750000000m, (await pay.HistoryAsync(actor, second.Id, CancellationToken.None))[0].Rate);
            var owner = actor with { IsAdmin = false, CanWriteEmployees = false, EmployeeId = second.Id };
            Assert.Single(await pay.HistoryAsync(owner, second.Id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pay.HistoryAsync(owner with { EmployeeId = first.Id }, second.Id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pay.HistoryAsync(actor with { IsAdmin = false, EmployeeId = null }, second.Id, CancellationToken.None));
            Assert.Empty(await pay.HistoryAsync(actor, outsider.Id, CancellationToken.None));
            Assert.DoesNotContain("Pay", string.Join(',', typeof(JobHistoryRow).GetProperties().Select(x => x.Name)));
            var photos = new EmployeePhotoService(db, TimeProvider.System);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4l8AAAAASUVORK5CYII=");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => photos.SaveAsync(actor with { IsAdmin = false, CanWriteEmployees = false }, second.Id, png, "image/png", null, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => photos.SaveAsync(actor, outsider.Id, png, "image/png", null, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => photos.SaveAsync(actor, second.Id, [1, 2, 3], "image/png", null, CancellationToken.None));
            await photos.SaveAsync(actor, second.Id, png, "image/png", null, CancellationToken.None);
            var summary = await photos.SummaryAsync(actor, second.Id, CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(png, (await photos.ReadAsync(actor, second.Id, CancellationToken.None))!.Bytes);
            Assert.Null(await photos.ReadAsync(actor, outsider.Id, CancellationToken.None));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => photos.SaveAsync(actor, second.Id, png, "image/png", [1], CancellationToken.None));
            await photos.DeleteAsync(actor, second.Id, summary.RowVersion, CancellationToken.None);
            Assert.Null(await photos.ReadAsync(actor, second.Id, CancellationToken.None));
            Assert.Equal(2, await db.Audits.CountAsync(x => x.EmployeeId == second.Id && (x.Action == "PhotoUpload" || x.Action == "PhotoDelete")));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => directory.NavigationAsync(actor with { IsAdmin = false, CanReadEmployees = false, CanWriteEmployees = false }, second.Id, CancellationToken.None));
        }
        finally
        {
            db.ChangeTracker.Clear();
            var ids = new[] { first.Id, second.Id, third.Id, outsider.Id };
            db.Photos.RemoveRange(await db.Photos.Where(x => ids.Contains(x.EmployeeId)).ToListAsync());
            db.EmployeeJobs.RemoveRange(await db.EmployeeJobs.Where(x => x.CompanyId == company.Id || x.CompanyId == foreign.Id).ToListAsync());
            db.Audits.RemoveRange(await db.Audits.Where(x => ids.Contains(x.EmployeeId)).ToListAsync());
            db.Employees.RemoveRange(await db.Employees.Where(x => ids.Contains(x.Id)).ToListAsync());
            await db.SaveChangesAsync();
            org.Departments.RemoveRange(department, otherDepartment);
            org.Companies.RemoveRange(company, foreign);
            await org.SaveChangesAsync();
        }
    }
}
