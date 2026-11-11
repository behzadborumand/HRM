using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Training;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlProfileActivityTests
{
    [Fact]
    public async Task LeaveProfileReservesAllPendingHoursAndPaginatesOnlyTheAuthorizedEmployee()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"))) return;
        await using var f = await ActivityFixture.CreateAsync();
        var policy = new LeavePolicy { CompanyId = f.Company.Id, Name = "Archived policy", AnnualHours = 160, Active = false };
        f.Leave.Policies.Add(policy);
        await f.Leave.SaveChangesAsync();
        f.Leave.Balances.AddRange(
            new LeaveBalance { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, PolicyId = policy.Id, Year = 2026, AccruedHours = 100, UsedHours = 24 },
            new LeaveBalance { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, PolicyId = policy.Id, Year = 2025, AccruedHours = 500, UsedHours = 0 });
        for (var i = 0; i < 24; i++)
            f.Leave.Requests.Add(new LeaveRequest { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, PolicyId = policy.Id,
                StartDate = new DateOnly(2026, 1, 1).AddDays(i), EndDate = new DateOnly(2026, 1, 1).AddDays(i),
                Hours = 8, Status = i == 0 ? "Pending" : "Approved", Reason = "PRIVATE_REASON" });
        f.Leave.Requests.Add(new LeaveRequest { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, PolicyId = policy.Id,
            StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 1, 1), Hours = 200, Status = "Pending" });
        f.Leave.Requests.Add(new LeaveRequest { CompanyId = f.Company.Id, EmployeeId = f.Other.Id, PolicyId = policy.Id,
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Hours = 200, Status = "Pending" });
        await f.Leave.SaveChangesAsync();
        var service = new LeaveProfileService(f.Leave, new PersonnelDirectory(f.Personnel), new LeaveYearClock());
        var result = await service.ReadAsync(f.Admin, f.Employee.Id, 1, default);
        Assert.NotNull(result);
        Assert.Equal(2026, result.Year);
        Assert.Equal(25, result.Total);
        Assert.Equal(20, result.Requests.Count);
        var balance = Assert.Single(result.Balances);
        Assert.Equal("Archived policy", balance.PolicyName);
        Assert.Equal(8, balance.PendingHours); // pending row is outside the first page
        Assert.Equal(68, balance.AvailableHours);
        Assert.Equal(new DateOnly(2026, 1, 24), result.Requests[0].StartDate);
        var last = await service.ReadAsync(f.Owner, f.Employee.Id, int.MaxValue, default);
        Assert.Equal(2, last!.Page);
        Assert.Equal(5, last.Requests.Count);
        Assert.Empty(result.Requests.Select(x => x.Id).Intersect(last.Requests.Select(x => x.Id)));
        Assert.Equal(1, (await service.ReadAsync(f.Owner, f.Employee.Id, -5, default))!.Page);
        await AssertDenied(service.ReadAsync, f);
        // Negative available balances must remain visible, rather than clamped to zero.
        f.Leave.Balances.Single(x => x.CompanyId == f.Company.Id && x.EmployeeId == f.Employee.Id && x.Year == 2026).AccruedHours = 20;
        await f.Leave.SaveChangesAsync();
        Assert.Equal(-12, Assert.Single((await service.ReadAsync(f.Admin, f.Employee.Id, 1, default))!.Balances).AvailableHours);
    }

    [Fact]
    public async Task TrainingProfileRetainsArchivedCoursesAndUsesTehranStatusWithStablePagination()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"))) return;
        await using var f = await ActivityFixture.CreateAsync();
        var course = new TrainingCourse { CompanyId = f.Company.Id, Title = "Archived safety", Provider = "Internal", Active = false };
        f.Training.Courses.Add(course);
        await f.Training.SaveChangesAsync();
        for (var i = 0; i < 24; i++)
            f.Training.Assignments.Add(new TrainingAssignment { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, CourseId = course.Id,
                AssignedDate = new DateOnly(2026, 1, 1).AddDays(i), DueDate = new DateOnly(2026, 11, 10),
                CompletedDate = new DateOnly(2026, 2, 1), CertificateNumber = "PRIVATE_CERT" });
        await f.Training.SaveChangesAsync();
        // Only one open assignment per course/employee is allowed; use a separate course for each boundary.
        foreach (var (title, due) in new[] { ("Due today", (DateOnly?)new DateOnly(2026, 11, 11)), ("Overdue", (DateOnly?)new DateOnly(2026, 11, 10)), ("No due date", (DateOnly?)null) })
        {
            var c = new TrainingCourse { CompanyId = f.Company.Id, Title = title };
            f.Training.Courses.Add(c); await f.Training.SaveChangesAsync();
            f.Training.Assignments.Add(new TrainingAssignment { CompanyId = f.Company.Id, EmployeeId = f.Employee.Id, CourseId = c.Id, AssignedDate = new DateOnly(2026, 1, 25), DueDate = due });
        }
        f.Training.Assignments.Add(new TrainingAssignment { CompanyId = f.Company.Id, EmployeeId = f.Other.Id, CourseId = course.Id, AssignedDate = new DateOnly(2026, 1, 25) });
        await f.Training.SaveChangesAsync();
        var service = new TrainingProfileService(f.Training, new PersonnelDirectory(f.Personnel), f.Clock);
        var result = await service.ReadAsync(f.Admin, f.Employee.Id, 1, default);
        Assert.NotNull(result);
        Assert.Equal(27, result.Total);
        Assert.Equal(20, result.Assignments.Count);
        Assert.Equal("Pending", result.Assignments.Single(x => x.CourseTitle == "Due today").Status);
        Assert.Equal("Overdue", result.Assignments.Single(x => x.CourseTitle == "Overdue").Status);
        Assert.Equal("Pending", result.Assignments.Single(x => x.CourseTitle == "No due date").Status);
        Assert.Equal("No due date", result.Assignments[0].CourseTitle); // equal-date ID descending
        var completed = result.Assignments.First(x => x.CourseTitle == "Archived safety");
        Assert.Equal("Completed", completed.Status);
        Assert.Equal("Internal", completed.Provider);
        var last = await service.ReadAsync(f.Owner, f.Employee.Id, int.MaxValue, default);
        Assert.Equal(2, last!.Page);
        Assert.Equal(7, last.Assignments.Count);
        Assert.Empty(result.Assignments.Select(x => x.Id).Intersect(last.Assignments.Select(x => x.Id)));
        Assert.Equal(1, (await service.ReadAsync(f.Owner, f.Employee.Id, 0, default))!.Page);
        await AssertDenied(service.ReadAsync, f);
    }

    private static async Task AssertDenied<T>(Func<ActorScope, int, int, CancellationToken, Task<T?>> read, ActivityFixture f) where T : class
    {
        foreach (var denied in new[] { f.Admin with { IsAdmin = false, EmployeeId = null },
            f.Owner with { EmployeeId = f.Other.Id }, f.Admin with { IsActive = false }, f.Owner with { IsActive = false } })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read(denied, f.Employee.Id, 1, default));
        Assert.Null(await read(f.Admin with { CompanyId = f.Foreign.Id }, f.Employee.Id, 1, default));
        Assert.Null(await read(f.Admin, int.MaxValue, 1, default));
        Assert.Null(await read(f.Owner with { CompanyId = f.Foreign.Id }, f.Employee.Id, 1, default));
    }

    private sealed class LeaveYearClock : TimeProvider
    {
        // UTC is still 2025; Tehran has entered 2026.
        public override DateTimeOffset GetUtcNow() => new(2025, 12, 31, 21, 0, 0, TimeSpan.Zero);
    }

    private sealed class TrialClock : TimeProvider
    {
        // UTC is November 10, Tehran is November 11.
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 10, 21, 0, 0, TimeSpan.Zero);
    }

    private sealed class ActivityFixture : IAsyncDisposable
    {
        public required OrganizationDbContext Org { get; init; }
        public required PersonnelDbContext Personnel { get; init; }
        public required LeaveDbContext Leave { get; init; }
        public required TrainingDbContext Training { get; init; }
        public required Company Company { get; init; }
        public required Company Foreign { get; init; }
        public required Department Department { get; init; }
        public required Employee Employee { get; init; }
        public required Employee Other { get; init; }
        public ActorScope Admin => new(51, "activity-admin", Company.Id, true, true, true, true);
        public ActorScope Owner => Admin with { IsAdmin = false, EmployeeId = Employee.Id };
        public TimeProvider Clock { get; } = new TrialClock();
        public static async Task<ActivityFixture> CreateAsync()
        {
            var c = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION")!;
            if (new SqlConnectionStringBuilder(c).InitialCatalog != "HRMS_Next_Trial_20261108") throw new InvalidOperationException("Trial database only.");
            var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(c).Options);
            var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(c).Options);
            var leave = new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(c).Options);
            var training = new TrainingDbContext(new DbContextOptionsBuilder<TrainingDbContext>().UseSqlServer(c).Options);
            var company = new Company { Name = "Profile activity " + Guid.NewGuid().ToString("N") };
            var foreign = new Company { Name = "Foreign activity " + Guid.NewGuid().ToString("N") };
            org.Companies.AddRange(company, foreign); await org.SaveChangesAsync();
            var department = new Department { CompanyId = company.Id, Name = "Activity" };
            org.Departments.Add(department); await org.SaveChangesAsync();
            Employee Person() => new() { CompanyId = company.Id, DepartmentId = department.Id, EmployeeNumber = Guid.NewGuid().ToString("N"), FirstName = "Activity", LastName = "Test", HireDate = new DateOnly(2020, 1, 1) };
            var employee = Person(); var other = Person();
            personnel.Employees.AddRange(employee, other); await personnel.SaveChangesAsync();
            return new() { Org = org, Personnel = personnel, Leave = leave, Training = training, Company = company, Foreign = foreign, Department = department, Employee = employee, Other = other };
        }

        public async ValueTask DisposeAsync()
        {
            var companyId = Company.Id;
            Leave.ChangeTracker.Clear(); Training.ChangeTracker.Clear();
            await Training.Assignments.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Training.Courses.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Leave.Requests.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Leave.Balances.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Leave.Policies.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Personnel.Employees.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Org.Departments.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
            await Org.Companies.Where(x => x.Id == companyId || x.Id == Foreign.Id).ExecuteDeleteAsync();
            await Training.DisposeAsync(); await Leave.DisposeAsync(); await Personnel.DisposeAsync(); await Org.DisposeAsync();
        }
    }
}
