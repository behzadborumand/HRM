using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Reporting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlHomeHighlightsTests
{
    [Fact]
    public async Task HomeHighlightsAreScopedAndRequirePeopleRead()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var people = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var leave = new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(connection).Options);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        var company = new Company { Name = "Home " + Guid.NewGuid().ToString("N") };
        var foreign = new Company { Name = "Foreign home " + Guid.NewGuid().ToString("N") };
        org.Companies.AddRange(company, foreign);
        await org.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "First" };
        var otherDepartment = new Department { CompanyId = foreign.Id, Name = "Other" };
        org.Departments.AddRange(department, otherDepartment);
        await org.SaveChangesAsync();
        var employee = new Employee { CompanyId = company.Id, DepartmentId = department.Id, EmployeeNumber = "HM-" + Guid.NewGuid().ToString("N")[..12], FirstName = "First", LastName = "Person", HireDate = today };
        var outsider = new Employee { CompanyId = foreign.Id, DepartmentId = otherDepartment.Id, EmployeeNumber = "HM-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Other", LastName = "Person", HireDate = today };
        people.Employees.AddRange(employee, outsider);
        await people.SaveChangesAsync();
        var policy = new LeavePolicy { CompanyId = company.Id, Name = "Annual", AnnualHours = 100 };
        leave.Policies.Add(policy);
        await leave.SaveChangesAsync();
        leave.Requests.AddRange(
            new LeaveRequest { CompanyId = company.Id, EmployeeId = employee.Id, PolicyId = policy.Id, StartDate = today, EndDate = today, Hours = 8, Status = "Approved", RequestedAtUtc = DateTimeOffset.UtcNow },
            new LeaveRequest { CompanyId = company.Id, EmployeeId = employee.Id, PolicyId = policy.Id, StartDate = today, EndDate = today, Hours = 8, Status = "Pending", RequestedAtUtc = DateTimeOffset.UtcNow });
        await leave.SaveChangesAsync();
        try
        {
            var service = new HomeHighlightsService(people, leave, org, TimeProvider.System);
            var actor = new ActorScope(1, "admin", company.Id, true, true, true, true);
            var snapshot = await service.LoadAsync(actor, CancellationToken.None);
            Assert.Single(snapshot.NewHires);
            Assert.Equal(employee.Id, snapshot.NewHires[0].EmployeeId);
            Assert.Single(snapshot.AbsenceDays[0].People);
            Assert.Equal(employee.Id, snapshot.AbsenceDays[0].People[0].EmployeeId);
            Assert.Single(snapshot.PendingRequests);
            Assert.Equal(employee.Id, snapshot.PendingRequests[0].EmployeeId);
            var reader = await service.LoadAsync(actor with { IsAdmin = false }, CancellationToken.None);
            Assert.Empty(reader.PendingRequests);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(actor with { IsAdmin = false, CanReadEmployees = false, CanWriteEmployees = false }, CancellationToken.None));
        }
        finally
        {
            leave.ChangeTracker.Clear();
            leave.Requests.RemoveRange(await leave.Requests.Where(x => x.CompanyId == company.Id).ToListAsync());
            leave.Policies.Remove(policy);
            await leave.SaveChangesAsync();
            people.ChangeTracker.Clear();
            people.Employees.RemoveRange(employee, outsider);
            await people.SaveChangesAsync();
            org.Departments.RemoveRange(department, otherDepartment);
            org.Companies.RemoveRange(company, foreign);
            await org.SaveChangesAsync();
        }
    }
}
