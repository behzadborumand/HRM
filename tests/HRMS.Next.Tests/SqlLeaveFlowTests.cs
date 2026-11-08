using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlLeaveFlowTests
{
    [Fact]
    public async Task RequestApprovalReservesAndConsumesBalanceWithinCompany()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests may run only against HRMS_Next_Trial_20261108.");
        await using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var leave = new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Leave test " + Guid.NewGuid().ToString("N") };
        organization.Companies.Add(company);
        await organization.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "Test" };
        organization.Departments.Add(department);
        await organization.SaveChangesAsync();
        var employee = new Employee { CompanyId = company.Id, DepartmentId = department.Id, EmployeeNumber = "LV-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Test", LastName = "Employee", HireDate = new DateOnly(2025, 1, 1) };
        personnel.Employees.Add(employee);
        await personnel.SaveChangesAsync();
        var policy = new LeavePolicy { CompanyId = company.Id, Name = "Annual", AnnualHours = 240m };
        leave.Policies.Add(policy);
        await leave.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(501, "leave-admin", company.Id, true, true, true, true);
            var service = new LeaveService(leave, new PersonnelDirectory(personnel), TimeProvider.System);
            var start = new DateOnly(2026, 11, 7);
            var end = new DateOnly(2026, 11, 8);
            var id = await service.CreateRequestAsync(actor, employee.Id, policy.Id, start, end, "Test", CancellationToken.None);
            Assert.Equal("Pending", (await leave.Requests.FindAsync(id))!.Status);
            Assert.Equal(224m, (await leave.Balances.SingleAsync()).AccruedHours - (await leave.Balances.SingleAsync()).UsedHours - (await leave.Requests.SingleAsync()).Hours);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRequestAsync(actor, employee.Id, policy.Id, start, end, null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DecideAsync(actor with { CompanyId = company.Id + 1 }, id, true, null, CancellationToken.None));
            await service.DecideAsync(actor, id, true, "Approved", CancellationToken.None);
            leave.ChangeTracker.Clear();
            Assert.Equal("Approved", (await leave.Requests.FindAsync(id))!.Status);
            Assert.Equal(16m, (await leave.Balances.SingleAsync()).UsedHours);
            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(actor, id, true, null, CancellationToken.None));
        }
        finally
        {
            leave.ChangeTracker.Clear();
            leave.Requests.RemoveRange(await leave.Requests.Where(x => x.CompanyId == company.Id).ToListAsync());
            leave.Balances.RemoveRange(await leave.Balances.Where(x => x.CompanyId == company.Id).ToListAsync());
            await leave.SaveChangesAsync();
            leave.Policies.Remove(policy);
            await leave.SaveChangesAsync();
            personnel.Employees.Remove(employee);
            await personnel.SaveChangesAsync();
            organization.Departments.Remove(department);
            organization.Companies.Remove(company);
            await organization.SaveChangesAsync();
        }
    }
}