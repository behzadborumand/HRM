using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlJobChangeTests
{
    [Fact]
    public async Task ChangeClosesOldJobPreservesPayAndRejectsInvalidWrites()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests may run only against HRMS_Next_Trial_20261108.");
        var orgOptions = new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options;
        var peopleOptions = new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options;
        await using var org = new OrganizationDbContext(orgOptions);
        await using var people = new PersonnelDbContext(peopleOptions);
        var company = new Company { Name = "Job Change " + Guid.NewGuid().ToString("N") };
        var otherCompany = new Company { Name = "Other Job " + Guid.NewGuid().ToString("N") };
        org.Companies.AddRange(company, otherCompany);
        await org.SaveChangesAsync();
        var first = new Department { CompanyId = company.Id, Name = "اول" };
        var second = new Department { CompanyId = company.Id, Name = "دوم" };
        var foreign = new Department { CompanyId = otherCompany.Id, Name = "غیرمجاز" };
        org.Departments.AddRange(first, second, foreign);
        await org.SaveChangesAsync();
        var employee = new Employee { CompanyId = company.Id, DepartmentId = first.Id, EmployeeNumber = "JC-" + Guid.NewGuid().ToString("N")[..12], FirstName = "One", LastName = "Employee", HireDate = new DateOnly(2020, 1, 1) };
        var manager = new Employee { CompanyId = company.Id, DepartmentId = first.Id, EmployeeNumber = "JC-" + Guid.NewGuid().ToString("N")[..12], FirstName = "One", LastName = "Manager", HireDate = new DateOnly(2020, 1, 1) };
        var foreignManager = new Employee { CompanyId = otherCompany.Id, DepartmentId = foreign.Id, EmployeeNumber = "JC-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Other", LastName = "Manager", HireDate = new DateOnly(2020, 1, 1) };
        people.Employees.AddRange(employee, manager, foreignManager);
        await people.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(91, "writer", company.Id, true, false, true, true);
            var service = new EmployeeJobService(people, new OrganizationDirectory(org), TimeProvider.System);
            var initial = new JobChangeInput(first.Id, "Junior", manager.Id, "FullTime", new DateOnly(2024, 1, 1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChangeAsync(actor with { CanWriteEmployees = false }, employee.Id, initial, employee.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(actor, employee.Id, initial with { DepartmentId = foreign.Id }, employee.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(actor, employee.Id, initial with { ManagerEmployeeId = foreignManager.Id }, employee.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(actor, employee.Id, initial with { ManagerEmployeeId = employee.Id }, employee.RowVersion, CancellationToken.None));
            Assert.Empty(await people.EmployeeJobs.Where(x => x.EmployeeId == employee.Id).ToListAsync());

            await service.ChangeAsync(actor, employee.Id, initial, employee.RowVersion, CancellationToken.None);
            var employeeService = new EmployeeService(people, new OrganizationDirectory(org), TimeProvider.System);
            var afterInitial = await people.Employees.AsNoTracking().SingleAsync(x => x.Id == employee.Id);
            var invalidHireDate = new EmployeeInput(first.Id, employee.EmployeeNumber, employee.FirstName,
                employee.LastName, null, initial.EffectiveDate.AddDays(1));
            await Assert.ThrowsAsync<ArgumentException>(() => employeeService.UpdateAsync(actor, employee.Id,
                invalidHireDate, afterInitial.RowVersion, CancellationToken.None));            var original = await people.EmployeeJobs.SingleAsync(x => x.EmployeeId == employee.Id);
            original.PayType = "Salary";
            original.PayRate = 123m;
            await people.SaveChangesAsync();
            original.IsCurrent = false;
            original.EndDate = new DateOnly(2025, 1, 1);
            await people.SaveChangesAsync();
            var gapVersion = (await people.Employees.AsNoTracking().SingleAsync(x => x.Id == employee.Id)).RowVersion;
            await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(actor, employee.Id,
                initial with { EffectiveDate = new DateOnly(2024, 6, 1) }, gapVersion, CancellationToken.None));
            original.IsCurrent = true;
            original.EndDate = null;
            await people.SaveChangesAsync();
            var version = (await people.Employees.AsNoTracking().SingleAsync(x => x.Id == employee.Id)).RowVersion;
            var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var changed = new JobChangeInput(second.Id, "Senior", manager.Id, "FullTime", effectiveDate);
            await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(actor, employee.Id, changed with { EffectiveDate = initial.EffectiveDate }, version, CancellationToken.None));
            await service.ChangeAsync(actor, employee.Id, changed, version, CancellationToken.None);
            people.ChangeTracker.Clear();
            var jobs = await people.EmployeeJobs.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.StartDate).ToListAsync();
            Assert.Equal(2, jobs.Count);
            Assert.False(jobs[0].IsCurrent);
            Assert.Equal(effectiveDate.AddDays(-1), jobs[0].EndDate);
            Assert.True(jobs[1].IsCurrent);
            Assert.Equal(second.Id, jobs[1].DepartmentId);
            Assert.Equal("Senior", jobs[1].JobTitle);
            Assert.Equal(123m, jobs[1].PayRate);
            Assert.Equal("Salary", jobs[1].PayType);
            Assert.Equal(second.Id, (await people.Employees.SingleAsync(x => x.Id == employee.Id)).DepartmentId);
            Assert.Equal(2, await people.Audits.CountAsync(x => x.EmployeeId == employee.Id && x.Action == "JobChange"));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ChangeAsync(actor, employee.Id, changed with { EffectiveDate = effectiveDate.AddDays(1) }, version, CancellationToken.None));
            Assert.Equal(2, await people.EmployeeJobs.CountAsync(x => x.EmployeeId == employee.Id));
        }
        finally
        {
            people.ChangeTracker.Clear();
            people.EmployeeJobs.RemoveRange(await people.EmployeeJobs.Where(x => x.EmployeeId == employee.Id).ToListAsync());
            people.Audits.RemoveRange(await people.Audits.Where(x => x.EmployeeId == employee.Id).ToListAsync());
            people.Employees.RemoveRange(await people.Employees.Where(x => x.Id == employee.Id || x.Id == manager.Id || x.Id == foreignManager.Id).ToListAsync());
            await people.SaveChangesAsync();
            org.Departments.RemoveRange(first, second, foreign);
            org.Companies.RemoveRange(company, otherCompany);
            await org.SaveChangesAsync();
        }
    }
}