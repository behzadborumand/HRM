using System.Security.Cryptography;
using System.Text.Json;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlDirectoryTests
{
    [Fact]
    public async Task CurrentTitleManagerPortraitAreSafeAndTenantScoped()
    {
        if (await Fixture.Create() is not { } f) return;
        await using var fixture = f;
        var manager = await f.Person("Manager");
        var employee = await f.Person("Employee");
        var foreign = await f.Person("FOREIGN_NAME", foreign: true);
        var wrongMetadata = await f.Person("Wrong metadata");
        var crossManager = await f.Person("Cross manager");
        f.People.EmployeeJobs.AddRange(
            new EmployeeJob { CompanyId = f.CompanyId, EmployeeId = employee.Id, StartDate = new(2020, 1, 1),
                EndDate = new(2020, 12, 31), JobTitle = "OLD_TITLE", PayRate = 999999999.99m },
            new EmployeeJob { CompanyId = f.CompanyId, EmployeeId = employee.Id, StartDate = new(2021, 1, 1),
                IsCurrent = true, JobTitle = "CURRENT_TITLE", ManagerEmployeeId = manager.Id, PayRate = 777777777.77m },
            new EmployeeJob { CompanyId = f.ForeignId, EmployeeId = wrongMetadata.Id, StartDate = new(2020, 1, 1),
                IsCurrent = true, JobTitle = "FOREIGN_TITLE" },
            new EmployeeJob { CompanyId = f.CompanyId, EmployeeId = crossManager.Id, StartDate = new(2020, 1, 1),
                IsCurrent = true, JobTitle = "LOCAL_TITLE", ManagerEmployeeId = foreign.Id });
        f.People.Photos.AddRange(f.Photo(employee.Id, f.CompanyId), f.Photo(wrongMetadata.Id, f.ForeignId));
        f.People.PersonalInfos.Add(new EmployeePersonalInfo { EmployeeId = employee.Id, CompanyId = f.CompanyId,
            DateOfBirth = new(1990, 1, 1), MobilePhone = "PRIVATE_DIRECTORY_PHONE", AddressLine1 = "PRIVATE_DIRECTORY_ADDRESS" });
        await f.People.SaveChangesAsync();
        f.People.ChangeTracker.Clear();

        var page = await f.Service.SearchAsync(f.Actor, null, null, 1, 20, CancellationToken.None);
        Assert.Equal(4, page.Total);
        var row = Assert.Single(page.Rows, x => x.Id == employee.Id);
        Assert.Equal("CURRENT_TITLE", row.JobTitle);
        Assert.Equal("Manager Person", row.ManagerName);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Fixture.Png)), row.PhotoSha256);
        Assert.Null(page.Rows.Single(x => x.Id == manager.Id).JobTitle);
        Assert.Null(page.Rows.Single(x => x.Id == manager.Id).PhotoSha256);
        Assert.Null(page.Rows.Single(x => x.Id == wrongMetadata.Id).JobTitle);
        Assert.Null(page.Rows.Single(x => x.Id == wrongMetadata.Id).PhotoSha256);
        Assert.Null(page.Rows.Single(x => x.Id == crossManager.Id).ManagerName);
        var json = JsonSerializer.Serialize(page);
        Assert.DoesNotContain("OLD_TITLE", json);
        Assert.DoesNotContain("FOREIGN_", json);
        Assert.DoesNotContain("PRIVATE_DIRECTORY", json);
        Assert.DoesNotContain("777777777", json);
        Assert.DoesNotContain("PayRate", json);
        Assert.DoesNotContain("DateOfBirth", json);
        Assert.DoesNotContain("Bytes", json);
        Assert.Empty(f.People.ChangeTracker.Entries());
        foreach (var actor in new[] { f.Actor with { CanReadEmployees = false, EmployeeId = employee.Id },
            f.Actor with { IsActive = false }, f.Actor with { CanReadEmployees = false, CanManageTeam = true, EmployeeId = manager.Id } })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SearchAsync(actor, null, null, 1, 20, CancellationToken.None));
        Assert.Equal(4, (await f.Service.SearchAsync(f.Actor with { CanReadEmployees = false, CanWriteEmployees = true }, null, null, 1, 20, CancellationToken.None)).Total);
    }

    [Fact]
    public async Task PageMetadataDoesNotChangeOrderingFiltersOrBounds()
    {
        if (await Fixture.Create() is not { } f) return;
        await using var fixture = f;
        var ids = new List<int>();
        for (var i = 0; i < 45; i++) ids.Add((await f.Person("Same", departed: i == 44)).Id);
        await f.Person("Same", foreign: true);
        for (var i = 0; i < 5; i++)
            f.People.EmployeeJobs.Add(new EmployeeJob { CompanyId = f.CompanyId, EmployeeId = ids[0],
                StartDate = new DateOnly(2020, 1, 1).AddDays(i), EndDate = new DateOnly(2020, 1, 1).AddDays(i),
                JobTitle = "History" });
        await f.People.SaveChangesAsync();
        f.People.ChangeTracker.Clear();
        var first = await f.Service.SearchAsync(f.Actor, null, null, 1, 20, CancellationToken.None);
        var second = await f.Service.SearchAsync(f.Actor, null, null, 2, 20, CancellationToken.None);
        var last = await f.Service.SearchAsync(f.Actor, null, null, int.MaxValue, 20, CancellationToken.None);
        Assert.Equal(45, first.Total);
        Assert.Equal(ids.Take(20), first.Rows.Select(x => x.Id));
        Assert.Equal(ids.Skip(20).Take(20), second.Rows.Select(x => x.Id));
        Assert.Equal(3, last.Page);
        Assert.Equal(ids.Skip(40), last.Rows.Select(x => x.Id));
        Assert.Equal(44, (await f.Service.SearchAsync(f.Actor, null, f.DepartmentId, 2, 20, CancellationToken.None, "Active")).Total);
        Assert.Single((await f.Service.SearchAsync(f.Actor, null, f.DepartmentId, 1, 20, CancellationToken.None, "Terminated")).Rows);
        Assert.All(first.Rows, x => Assert.Null(x.JobTitle));
        Assert.Empty(f.People.ChangeTracker.Entries());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4l8AAAAASUVORK5CYII=");
        private readonly OrganizationDbContext org;
        public PersonnelDbContext People { get; }
        public int CompanyId { get; }
        public int ForeignId { get; }
        public int DepartmentId { get; }
        private readonly int foreignDepartment;
        public ActorScope Actor => new(1, "directory-reader", CompanyId, true, false, true, false);
        public EmployeeService Service => new(People, new OrganizationDirectory(org), TimeProvider.System);
        private Fixture(OrganizationDbContext org, PersonnelDbContext people, int company, int foreign, int department, int other)
        { this.org = org; People = people; CompanyId = company; ForeignId = foreign; DepartmentId = department; foreignDepartment = other; }

        public static async Task<Fixture?> Create()
        {
            var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection)) return null;
            if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the named trial database is allowed.");
            var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
            var people = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
            var company = new Company { Name = "Directory_" + Guid.NewGuid().ToString("N") };
            var foreign = new Company { Name = "DirectoryForeign_" + Guid.NewGuid().ToString("N") };
            org.Companies.AddRange(company, foreign); await org.SaveChangesAsync();
            var department = new Department { CompanyId = company.Id, Name = "Local" };
            var other = new Department { CompanyId = foreign.Id, Name = "Foreign" };
            org.Departments.AddRange(department, other); await org.SaveChangesAsync();
            return new(org, people, company.Id, foreign.Id, department.Id, other.Id);
        }
        public async Task<Employee> Person(string name, bool foreign = false, bool departed = false)
        {
            var employee = new Employee { CompanyId = foreign ? ForeignId : CompanyId,
                DepartmentId = foreign ? foreignDepartment : DepartmentId, EmployeeNumber = Guid.NewGuid().ToString("N"),
                FirstName = name, LastName = "Person", HireDate = new(2020, 1, 1),
                TerminationDate = departed ? new DateOnly(2024, 1, 1) : null };
            People.Employees.Add(employee); await People.SaveChangesAsync(); People.ChangeTracker.Clear(); return employee;
        }
        public EmployeePhoto Photo(int id, int company) => new() { EmployeeId = id, CompanyId = company,
            Bytes = Png, ContentType = "image/png", Sha256 = Convert.ToHexString(SHA256.HashData(Png)), UploadedAtUtc = DateTimeOffset.UtcNow };
        public async ValueTask DisposeAsync()
        {
            People.ChangeTracker.Clear();
            await People.EmployeeJobs.Where(x => x.CompanyId == CompanyId || x.CompanyId == ForeignId).ExecuteDeleteAsync();
            await People.Photos.Where(x => x.CompanyId == CompanyId || x.CompanyId == ForeignId).ExecuteDeleteAsync();
            await People.PersonalInfos.Where(x => x.CompanyId == CompanyId || x.CompanyId == ForeignId).ExecuteDeleteAsync();
            await People.Employees.Where(x => x.CompanyId == CompanyId || x.CompanyId == ForeignId).ExecuteDeleteAsync();
            await org.Departments.Where(x => x.CompanyId == CompanyId || x.CompanyId == ForeignId).ExecuteDeleteAsync();
            await org.Companies.Where(x => x.Id == CompanyId || x.Id == ForeignId).ExecuteDeleteAsync();
            await People.DisposeAsync(); await org.DisposeAsync();
        }
    }
}
