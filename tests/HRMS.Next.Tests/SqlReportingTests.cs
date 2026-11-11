using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Reporting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlReportingTests
{
    [Fact]
    public async Task ReportsOnlyShowTheActorsCompanyAndDoNotIncludePay()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var people = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var leave = new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Reporting " + Guid.NewGuid().ToString("N") };
        var other = new Company { Name = "Other reporting " + Guid.NewGuid().ToString("N") };
        org.Companies.AddRange(company, other);
        await org.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "First" };
        var otherDepartment = new Department { CompanyId = other.Id, Name = "Other" };
        org.Departments.AddRange(department, otherDepartment);
        await org.SaveChangesAsync();
        var employee = new Employee { CompanyId = company.Id, DepartmentId = department.Id, EmployeeNumber = "RP-" + Guid.NewGuid().ToString("N")[..12], FirstName = "First", LastName = "Person", HireDate = new DateOnly(2024, 1, 1) };
        var foreignEmployee = new Employee { CompanyId = other.Id, DepartmentId = otherDepartment.Id, EmployeeNumber = "RP-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Other", LastName = "Person", HireDate = new DateOnly(2024, 1, 1) };
        people.Employees.AddRange(employee, foreignEmployee);
        await people.SaveChangesAsync();
        var policy = new LeavePolicy { CompanyId = company.Id, Name = "Annual", AnnualHours = 100 };
        leave.Policies.Add(policy);
        await leave.SaveChangesAsync();
        leave.Balances.Add(new LeaveBalance { CompanyId = company.Id, EmployeeId = employee.Id, PolicyId = policy.Id, Year = 2026, AccruedHours = 100, UsedHours = 10, UpdatedAtUtc = DateTimeOffset.UtcNow });
        leave.Requests.Add(new LeaveRequest { CompanyId = company.Id, EmployeeId = employee.Id, PolicyId = policy.Id, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Hours = 8, Status = "Pending", RequestedAtUtc = DateTimeOffset.UtcNow });
        await leave.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(1, "admin", company.Id, true, true, true, true);
            var reports = new ReportingService(people, leave, org, TimeProvider.System);
            Assert.Equal(1, (await reports.DashboardAsync(actor, CancellationToken.None)).TotalEmployees);
            Assert.Equal(1, (await reports.DashboardAsync(actor, CancellationToken.None)).PendingRequests);
            var page = await reports.EmployeesAsync(actor, null, null, null, 1, 20, CancellationToken.None);
            Assert.Single(page.Rows);
            Assert.Equal(employee.Id, page.Rows[0].EmployeeId);
            Assert.DoesNotContain("Pay", string.Join(',', typeof(EmployeeReportRow).GetProperties().Select(x => x.Name)));
            var balance = await reports.LeaveSummaryAsync(actor, 2026, null, 1, 20, CancellationToken.None);
            Assert.Single(balance.Rows);
            Assert.Equal(90m, balance.Rows[0].AvailableHours);
            Assert.Equal(1, balance.Rows[0].RequestCount);
            Assert.Single((await reports.LeaveSummaryAsync(actor, 2026, department.Id, 1, 20, CancellationToken.None)).Rows);
            Assert.Empty((await reports.LeaveSummaryAsync(actor, 2026, otherDepartment.Id, 1, 20, CancellationToken.None)).Rows);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reports.DashboardAsync(actor with { IsAdmin = false }, CancellationToken.None));
        }
        finally
        {
            leave.ChangeTracker.Clear();
            leave.Requests.RemoveRange(await leave.Requests.Where(x => x.CompanyId == company.Id).ToListAsync());
            leave.Balances.RemoveRange(await leave.Balances.Where(x => x.CompanyId == company.Id).ToListAsync());
            leave.Policies.Remove(policy);
            await leave.SaveChangesAsync();
            people.ChangeTracker.Clear();
            people.Employees.RemoveRange(employee, foreignEmployee);
            await people.SaveChangesAsync();
            org.Departments.RemoveRange(department, otherDepartment);
            org.Companies.RemoveRange(company, other);
            await org.SaveChangesAsync();
        }
    }
}
