using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlEmployeeFlowTests
{
    [Fact]
    public async Task CreateSearchEditConcurrencyAndAuditOnSqlServer()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests may run only against HRMS_Next_Trial_20261108.");

        var orgOptions = new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options;
        var personnelOptions = new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options;
        await using var organization = new OrganizationDbContext(orgOptions);
        await using var personnel = new PersonnelDbContext(personnelOptions);
        var company = new Company { Name = "Integration " + Guid.NewGuid().ToString("N") };
        organization.Companies.Add(company);
        await organization.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "آزمایش" };
        organization.Departments.Add(department);
        await organization.SaveChangesAsync();

        try
        {
            var actor = new ActorScope(77, "integration", company.Id, true, true, true, true);
            var service = new EmployeeService(personnel, new OrganizationDirectory(organization), TimeProvider.System);
            var input = new EmployeeInput(department.Id, "IT-" + Guid.NewGuid().ToString("N")[..12], "نام", "خانوادگی", "integration@example.test", new DateOnly(2024, 3, 20));
            var id = await service.CreateAsync(actor, input, CancellationToken.None);
            var page = await service.SearchAsync(actor, input.EmployeeNumber, department.Id, 1, 20, CancellationToken.None);
            Assert.Single(page.Rows);
            Assert.Equal(id, page.Rows[0].Id);
            Assert.Single((await service.SearchAsync(actor, "integration@example.test", null, 1, 20, CancellationToken.None)).Rows);
            Assert.Single((await service.SearchAsync(actor, null, null, 1, 20, CancellationToken.None, "Active")).Rows);
            Assert.Empty((await service.SearchAsync(actor, null, null, 1, 20, CancellationToken.None, "Terminated")).Rows);
            Assert.Null(await service.GetAsync(actor with { CompanyId = company.Id + 1 }, id, CancellationToken.None));
            var before = (await service.GetAsync(actor, id, CancellationToken.None))!;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(actor, id, input with { LastName = "ویرایش" }, [1, 2], CancellationToken.None));
            await service.UpdateAsync(actor, id, input with { LastName = "ویرایش" }, before.RowVersion, CancellationToken.None);
            Assert.Equal("ویرایش", (await service.GetAsync(actor, id, CancellationToken.None))!.LastName);
            Assert.Equal(2, await personnel.Audits.CountAsync(x => x.EmployeeId == id));
        }
        finally
        {
            var employees = await personnel.Employees.Where(x => x.CompanyId == company.Id).ToListAsync();
            var ids = employees.Select(x => x.Id).ToList();
            personnel.Audits.RemoveRange(await personnel.Audits.Where(x => ids.Contains(x.EmployeeId)).ToListAsync());
            personnel.Employees.RemoveRange(employees);
            await personnel.SaveChangesAsync();
            organization.Departments.Remove(department);
            organization.Companies.Remove(company);
            await organization.SaveChangesAsync();
        }
    }
}
