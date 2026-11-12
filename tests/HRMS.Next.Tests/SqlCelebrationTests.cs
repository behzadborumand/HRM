using System.Globalization;
using System.Text.Json;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlCelebrationTests
{
    [Fact]
    public async Task PersianWindowCrossesNowruzWithoutExposingBirthYear()
    {
        if (await Fixture.CreateAsync() is not { } f) return;
        await using var fixture = f;
        var today = new DateOnly(2025, 3, 19); //1403/12/29; next year starts March21.
        var current = await f.Add("Today", P(1390, 4, 1), P(1370, 12, 29));
        var leap = await f.Add("Leap", P(1390, 4, 1), P(1379, 12, 30));
        var anniversary = await f.Add("Anniversary", P(1400, 1, 1));
        var newYear = await f.Add("Nowruz", P(1390, 4, 1), P(1370, 1, 2));
        var boundary = await f.Add("Last", P(1390, 4, 1), P(1370, 1, 28));
        await f.Add("Outside", P(1390, 4, 1), P(1370, 1, 29));
        await f.Add("FirstDay", today);

        var rows = await f.Service.LoadAsync(f.Actor, today, CancellationToken.None);
        Assert.Equal(new[] { current.Id, leap.Id, anniversary.Id, newYear.Id, boundary.Id }, rows.Select(x => x.EmployeeId));
        Assert.Equal(new[] { today, new DateOnly(2025, 3, 20), new DateOnly(2025, 3, 21),
            new DateOnly(2025, 3, 22), new DateOnly(2025, 4, 17) }, rows.Select(x => x.Date));
        Assert.Equal(4, rows[2].Years);
        Assert.All(rows.Where(x => x.Kind == CelebrationKind.Birthday), x => Assert.Null(x.Years));
        //Observable serialization is the public projection: no original birth date/year or private fields.
        var json = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain("2001", json);
        Assert.DoesNotContain("1992", json);
        Assert.DoesNotContain("DateOfBirth", json);
        Assert.DoesNotContain("PRIVATE-MOBILE", json);
        Assert.DoesNotContain("PRIVATE-ADDRESS", json);
        Assert.Empty(f.People.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EsfandThirtyClampsAndRollsForward()
    {
        if (await Fixture.CreateAsync() is not { } f) return;
        await using var fixture = f;
        var person = await f.Add("Leap", P(1399, 12, 30), P(1379, 12, 30));
        var rows = await f.Service.LoadAsync(f.Actor, new DateOnly(2026, 3, 19), CancellationToken.None);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(new DateOnly(2026, 3, 20), x.Date)); //1404/12/29.
        Assert.Equal(CelebrationKind.Anniversary, rows[0].Kind);
        Assert.Equal(5, rows[0].Years);
        Assert.Equal(CelebrationKind.Birthday, rows[1].Kind);
        Assert.Null(rows[1].Years);
        Assert.All(rows, x => Assert.Equal(person.Id, x.EmployeeId));
        Assert.Empty(await f.Service.LoadAsync(f.Actor, new DateOnly(2026, 3, 21), CancellationToken.None));
    }

    [Fact]
    public async Task CompanyAndGrantBoundariesExcludePrivateAndInactivePeople()
    {
        if (await Fixture.CreateAsync() is not { } f) return;
        await using var fixture = f;
        var today = new DateOnly(2026, 11, 12);
        var good = await f.Add("Good", P(1400, 8, 21)); //No PersonalInfos; anniversary still visible.
        await f.Add("Foreign", P(1400, 8, 21), P(1370, 8, 21), foreign: true);
        await f.Add("Departed", P(1400, 8, 21), P(1370, 8, 21), departed: true);
        await f.Add("FutureHire", today.AddDays(1), P(1370, 8, 21));
        await f.Add("Unsupported", DateOnly.MinValue, DateOnly.MinValue);
        await f.Add("FutureBirth", P(1400, 4, 1), today.AddYears(1));

        foreach (var actor in new[] { f.Actor, f.Actor with { IsAdmin = true, CanReadEmployees = false },
            f.Actor with { CanReadEmployees = false, CanWriteEmployees = true } })
        {
            var result = await f.Service.LoadAsync(actor, today, CancellationToken.None);
            Assert.Equal(good.Id, Assert.Single(result).EmployeeId);
        }
        foreach (var actor in new[] { f.Actor with { CanReadEmployees = false, EmployeeId = good.Id },
            f.Actor with { CanReadEmployees = false, CanManageTeam = true, EmployeeId = good.Id },
            f.Actor with { IsActive = false }, f.Actor with { CompanyId = 0, IsAdmin = true },
            f.Actor with { CompanyId = -1, IsAdmin = true } })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.LoadAsync(actor, today, CancellationToken.None));
        Assert.Empty(f.People.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SixEventsHaveStableDateKindNameAndIdOrder()
    {
        if (await Fixture.CreateAsync() is not { } f) return;
        await using var fixture = f;
        var today = new DateOnly(2026, 11, 12);
        var z = await f.Add("Z", P(1400, 4, 1), P(1370, 8, 21));
        var a = await f.Add("A", P(1400, 4, 1), P(1370, 8, 21));
        var same1 = await f.Add("Same", P(1400, 4, 1), P(1370, 8, 21));
        var same2 = await f.Add("Same", P(1400, 4, 1), P(1370, 8, 21));
        var ann = await f.Add("Last", P(1400, 8, 21), P(1370, 8, 21));
        await f.Add("ZZ", P(1400, 4, 1), P(1370, 8, 21));
        await f.Add("ZZZ", P(1400, 4, 1), P(1370, 8, 21));
        var result = await f.Service.LoadAsync(f.Actor, today, CancellationToken.None);
        Assert.Equal(new[] { ann.Id, a.Id, ann.Id, same1.Id, same2.Id, z.Id }, result.Select(x => x.EmployeeId));
        Assert.Equal(CelebrationKind.Anniversary, result[0].Kind);
        Assert.All(result.Skip(1), x => Assert.Equal(CelebrationKind.Birthday, x.Kind));
        Assert.All(result, x => Assert.Equal(today, x.Date));
    }

    [Fact]
    public async Task HomeUsesTehranDayAcrossUtcMidnightBoundary()
    {
        if (await Fixture.CreateAsync() is not { } f) return;
        await using var fixture = f;
        var person = await f.Add("Yesterday", P(1400, 4, 1), P(1370, 8, 20));
        await using var leave = new HRMS.Next.Modules.Leave.LeaveDbContext(
            new DbContextOptionsBuilder<HRMS.Next.Modules.Leave.LeaveDbContext>()
                .UseSqlServer(f.People.Database.GetConnectionString()).Options);
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 11, 20, 0, 0, TimeSpan.Zero));
        var home = new HRMS.Next.Modules.Reporting.HomeHighlightsService(f.People, leave, f.Organization, clock, f.Service);
        var before = await home.LoadAsync(f.Actor, CancellationToken.None);
        Assert.Equal(person.Id, Assert.Single(before.Celebrations).EmployeeId);
        Assert.Equal(new DateOnly(2026, 11, 11), before.Celebrations[0].Date);
        clock.Now = new DateTimeOffset(2026, 11, 11, 21, 0, 0, TimeSpan.Zero); //Tehran Nov12 00:30.
        Assert.Empty((await home.LoadAsync(f.Actor, CancellationToken.None)).Celebrations);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }


    private static DateOnly P(int year, int month, int day) =>
        DateOnly.FromDateTime(new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly OrganizationDbContext org;
        private readonly Company company;
        private readonly Company foreign;
        private readonly Department department;
        private readonly Department foreignDepartment;
        public OrganizationDbContext Organization => org;
        public PersonnelDbContext People { get; }
        public EmployeeCelebrationService Service { get; }
        public ActorScope Actor => new(1, "reader", company.Id, true, false, true, false);

        private Fixture(OrganizationDbContext org, PersonnelDbContext people, Company company, Company foreign,
            Department department, Department foreignDepartment)
        {
            this.org = org; People = people; this.company = company; this.foreign = foreign;
            this.department = department; this.foreignDepartment = foreignDepartment;
            Service = new EmployeeCelebrationService(people);
        }
        public static async Task<Fixture?> CreateAsync()
        {
            var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection)) return null;
            if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog,
                "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the named trial database is allowed.");
            var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
            var people = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
            var company = new Company { Name = "Celebration_" + Guid.NewGuid().ToString("N") };
            var foreign = new Company { Name = "CelebrationForeign_" + Guid.NewGuid().ToString("N") };
            org.Companies.AddRange(company, foreign);
            await org.SaveChangesAsync();
            var department = new Department { CompanyId = company.Id, Name = "Local" };
            var other = new Department { CompanyId = foreign.Id, Name = "Foreign" };
            org.Departments.AddRange(department, other);
            await org.SaveChangesAsync();
            return new Fixture(org, people, company, foreign, department, other);
        }
        public async Task<Employee> Add(string name, DateOnly hire, DateOnly? birth = null, bool foreign = false, bool departed = false)
        {
            var employee = new Employee { CompanyId = foreign ? this.foreign.Id : company.Id,
                DepartmentId = foreign ? foreignDepartment.Id : department.Id, EmployeeNumber = Guid.NewGuid().ToString("N"),
                FirstName = name, LastName = "Person", HireDate = hire, TerminationDate = departed ? hire.AddDays(1) : null };
            People.Employees.Add(employee);
            await People.SaveChangesAsync();
            if (birth is not null)
            {
                People.PersonalInfos.Add(new EmployeePersonalInfo { EmployeeId = employee.Id, CompanyId = employee.CompanyId,
                    DateOfBirth = birth, MobilePhone = "PRIVATE-MOBILE", AddressLine1 = "PRIVATE-ADDRESS" });
                await People.SaveChangesAsync();
            }
            People.ChangeTracker.Clear();
            return employee;
        }
        public async ValueTask DisposeAsync()
        {
            People.ChangeTracker.Clear();
            await People.PersonalInfos.Where(x => x.CompanyId == company.Id || x.CompanyId == foreign.Id).ExecuteDeleteAsync();
            await People.Employees.Where(x => x.CompanyId == company.Id || x.CompanyId == foreign.Id).ExecuteDeleteAsync();
            await org.Departments.Where(x => x.CompanyId == company.Id || x.CompanyId == foreign.Id).ExecuteDeleteAsync();
            await org.Companies.Where(x => x.Id == company.Id || x.Id == foreign.Id).ExecuteDeleteAsync();
            await People.DisposeAsync();
            await org.DisposeAsync();
        }
    }
}
