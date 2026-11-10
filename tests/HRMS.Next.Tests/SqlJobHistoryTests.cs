using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlJobHistoryTests
{
    [Fact]
    public async Task HistoryIsCompanyScopedOrderedAndExcludesPay()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests may run only against HRMS_Next_Trial_20261108.");

        var options = new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options;
        await using var db = new PersonnelDbContext(options);
        var first = new Employee { CompanyId = 910001, DepartmentId = 1, EmployeeNumber = "JOB-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Test", LastName = "One", HireDate = new DateOnly(2020, 1, 1) };
        var other = new Employee { CompanyId = 910002, DepartmentId = 1, EmployeeNumber = "JOB-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Test", LastName = "Two", HireDate = new DateOnly(2020, 1, 1) };
        db.Employees.AddRange(first, other);
        await db.SaveChangesAsync();
        try
        {
            db.EmployeeJobs.AddRange(
                new EmployeeJob { CompanyId = first.CompanyId, EmployeeId = first.Id, StartDate = new DateOnly(2020, 1, 1), EndDate = new DateOnly(2022, 12, 31), JobTitle = "Old", PayRate = 100, IsCurrent = false },
                new EmployeeJob { CompanyId = first.CompanyId, EmployeeId = first.Id, StartDate = new DateOnly(2023, 1, 1), JobTitle = "New", PayRate = 200, IsCurrent = true },
                new EmployeeJob { CompanyId = other.CompanyId, EmployeeId = other.Id, StartDate = new DateOnly(2024, 1, 1), JobTitle = "Other", IsCurrent = true });
            await db.SaveChangesAsync();
            var service = new EmployeeJobService(db);
            var actor = new ActorScope(1, "tester", first.CompanyId, true, false, true, false);
            var rows = await service.HistoryAsync(actor, first.Id, CancellationToken.None);
            Assert.Equal(new[] { "New", "Old" }, rows.Select(x => x.JobTitle));
            Assert.Null(typeof(JobHistoryRow).GetProperty("PayRate"));
            Assert.Null(typeof(JobHistoryRow).GetProperty("PayType"));
            Assert.Empty(await service.HistoryAsync(actor, other.Id, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HistoryAsync(actor with { CanReadEmployees = false }, first.Id, CancellationToken.None));
        }
        finally
        {
            db.EmployeeJobs.RemoveRange(await db.EmployeeJobs.Where(x => x.EmployeeId == first.Id || x.EmployeeId == other.Id).ToListAsync());
            db.Employees.RemoveRange(first, other);
            await db.SaveChangesAsync();
        }
    }
}