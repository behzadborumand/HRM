using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Training;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlSelfServiceTests
{
    [Fact]
    public async Task LinkedOwnerWithoutDirectoryGrantsReadsOnlyOwnProfileFilesAndTraining()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"))) return;
        await using var f = await Fixture.CreateAsync();
        var employeeService = new EmployeeService(f.People, new OrganizationDirectory(f.Org), f.Clock);
        var jobs = new EmployeeJobService(f.People, new OrganizationDirectory(f.Org), f.Clock);
        var photos = new EmployeePhotoService(f.People, f.Clock);
        var documents = new DocumentService(f.Documents, new PersonnelDirectory(f.People), f.Clock);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4l8AAAAASUVORK5CYII=");
        await photos.SaveAsync(f.Admin, f.Self.Id, png, "image/png", null, default);
        f.People.EmployeeJobs.Add(new EmployeeJob { CompanyId=f.Company.Id, EmployeeId=f.Self.Id, DepartmentId=f.Department.Id, StartDate=f.Self.HireDate, IsCurrent=true, JobTitle="Analyst", EmploymentType="FullTime", PayType="Salary", PayRate=123.45m });
        await f.People.SaveChangesAsync();
        var bytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 self file");
        var ownDocument = await documents.UploadAsync(f.Admin, f.Self.Id, "Own document", null, null, "own.pdf", new MemoryStream(bytes), default);
        var peerDocument = await documents.UploadAsync(f.Admin, f.Peer.Id, "Peer secret", null, null, "peer.pdf", new MemoryStream(bytes), default);
        var training = new TrainingService(f.Training, new PersonnelDirectory(f.People), f.Clock);
        var course = await training.CreateCourseAsync(f.Admin, "Safety", null, null, null, null, false, default);
        var assignment = await training.AssignAsync(f.Admin, f.Self.Id, course, new DateOnly(2026,1,1), null, default);

        Assert.Equal(f.Self.Id, (await employeeService.GetAsync(f.Owner, f.Self.Id, default))!.Id);
        Assert.Single(await jobs.HistoryAsync(f.Owner, f.Self.Id, default));
        Assert.Equal(123.45m, Assert.Single(await new EmployeePayService(f.People).HistoryAsync(f.Owner, f.Self.Id, default)).Rate);
        Assert.NotNull(await photos.SummaryAsync(f.Owner, f.Self.Id, default));
        Assert.Equal(png, (await photos.ReadAsync(f.Owner, f.Self.Id, default))!.Bytes);
        Assert.Single((await documents.ListAsync(f.Owner, f.Self.Id, 1, 20, default)).Rows);
        Assert.Equal(bytes, (await documents.DownloadAsync(f.Owner, ownDocument, default))!.Bytes);
        Assert.Null(await documents.DownloadAsync(f.Owner, peerDocument, default));
        Assert.Null(await documents.DownloadAsync(f.Owner with { CompanyId=f.Foreign.Id }, ownDocument, default));
        var profile = await new TrainingProfileService(f.Training, new PersonnelDirectory(f.People), f.Clock).ReadAsync(f.Owner, f.Self.Id, 1, default);
        Assert.Equal(assignment, Assert.Single(profile!.Assignments).Id);
        foreach(var other in new[] { f.Peer.Id, f.Outsider.Id })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => employeeService.GetAsync(f.Owner, other, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => jobs.HistoryAsync(f.Owner, other, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => photos.ReadAsync(f.Owner, other, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => documents.ListAsync(f.Owner, other, 1, 20, default));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => employeeService.SearchAsync(f.Owner, null, null, 1, 20, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new EmployeeProfileDirectory(f.People).NavigationAsync(f.Owner, f.Self.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => photos.SaveAsync(f.Owner, f.Self.Id, png, "image/png", null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => documents.DeleteAsync(f.Owner, ownDocument, f.Self.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => training.CompleteAsync(f.Owner, assignment, new DateOnly(2026,11,11), null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => employeeService.GetAsync(f.Owner with { IsActive=false }, f.Self.Id, default));
    }

    [Fact]
    public async Task OwnerRequestsOnlyOwnLeaveAndCancelsPendingWithExactVersionWithoutApprovalRights()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"))) return;
        await using var f = await Fixture.CreateAsync();
        var service = new LeaveService(f.Leave, new PersonnelDirectory(f.People), f.Clock);
        var policy = await service.CreatePolicyAsync(f.Admin, "Annual", 16, null, null, default);
        var foreignPolicy = await service.CreatePolicyAsync(f.Admin with { CompanyId=f.Foreign.Id }, "Foreign", 100, null, null, default);
        var peerRequest = await service.CreateRequestAsync(f.Admin, f.Peer.Id, policy, new(2026,11,14), new(2026,11,14), null, default);
        var request = await service.CreateRequestAsync(f.Owner, f.Self.Id, policy, new(2026,11,14), new(2026,11,15), "private", default);
        var stored = await f.Leave.Requests.SingleAsync(x=>x.Id==request);
        Assert.Equal(f.Owner.Id, stored.RequestedByUserId);
        Assert.Equal(new DateTimeOffset(2026,11,11,10,0,0,TimeSpan.Zero), stored.RequestedAtUtc);
        Assert.Equal(16m, stored.Hours);
        Assert.Equal(policy, Assert.Single(await service.RequestPoliciesAsync(f.Owner, default)).Id);
        var pending = await service.PendingOwnAsync(f.Owner, 1, default);
        Assert.Equal(request, Assert.Single(pending.Rows).Id);
        foreach(var denied in new[] { f.Owner with { IsActive=false }, f.Owner with { EmployeeId=null }, f.Owner with { EmployeeId=f.Peer.Id } })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateRequestAsync(denied, f.Self.Id, policy, new(2026,11,16), new(2026,11,16), null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateRequestAsync(f.Owner, f.Peer.Id, policy, new(2026,11,16), new(2026,11,16), null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRequestAsync(f.Owner, f.Self.Id, foreignPolicy, new(2026,11,16), new(2026,11,16), null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRequestAsync(f.Owner, f.Self.Id, policy, new(2026,11,14), new(2026,11,14), null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRequestAsync(f.Owner, f.Self.Id, policy, new(2026,11,16), new(2026,11,16), null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DecideAsync(f.Owner, request, true, null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreatePolicyAsync(f.Owner, "Forbidden", 1, null, null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CancelAsync(f.Owner, request, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CancelOwnAsync(f.Owner, peerRequest, stored.RowVersion, default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.CancelOwnAsync(f.Owner, request, [0], default));
        await service.CancelOwnAsync(f.Owner, request, stored.RowVersion.ToArray(), default);
        f.Leave.ChangeTracker.Clear();
        var cancelled = await f.Leave.Requests.SingleAsync(x=>x.Id==request);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(f.Owner.Id, cancelled.DecidedByUserId);
        Assert.Equal(new DateTimeOffset(2026,11,11,10,0,0,TimeSpan.Zero), cancelled.DecidedAtUtc);
        Assert.Empty((await service.PendingOwnAsync(f.Owner, int.MaxValue, default)).Rows);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CancelOwnAsync(f.Owner, request, cancelled.RowVersion, default));
        var approved = await service.CreateRequestAsync(f.Owner, f.Self.Id, policy, new(2026,11,14), new(2026,11,15), null, default);
        await service.DecideAsync(f.Admin, approved, true, null, default);
        var version = (await f.Leave.Requests.SingleAsync(x=>x.Id==approved)).RowVersion;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CancelOwnAsync(f.Owner, approved, version, default));
        foreach(var denied in new[] { f.Owner with { IsActive=false }, f.Owner with { EmployeeId=null }, f.Owner with { CompanyId=f.Foreign.Id } })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RequestPoliciesAsync(denied, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PendingOwnAsync(denied, 1, default));
        }
        for(var i=0;i<21;i++)
            f.Leave.Requests.Add(new LeaveRequest { CompanyId=f.Company.Id,EmployeeId=f.Self.Id,PolicyId=policy,StartDate=new DateOnly(2026,1,1).AddDays(i),EndDate=new DateOnly(2026,1,1).AddDays(i),Hours=8,Status="Pending" });
        await f.Leave.SaveChangesAsync();
        var firstPending=await service.PendingOwnAsync(f.Owner, 1, default);
        var lastPending=await service.PendingOwnAsync(f.Owner, int.MaxValue, default);
        Assert.Equal(21, firstPending.Total);
        Assert.Equal(20, firstPending.Rows.Count);
        Assert.Equal(2, lastPending.Page);
        Assert.Equal(new DateOnly(2026,1,1), Assert.Single(lastPending.Rows).StartDate);
        Assert.All(firstPending.Rows, row => Assert.NotEmpty(row.RowVersion));
        Assert.Empty(firstPending.Rows.Select(x=>x.Id).Intersect(lastPending.Rows.Select(x=>x.Id)));
        f.Self.TerminationDate = new DateOnly(2026,11,10); await f.People.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRequestAsync(f.Owner, f.Self.Id, policy, new(2026,11,9), new(2026,11,9), null, default));
    }

    [Fact]
    public async Task ConcurrentApprovalsRetryWholeDeadlockedTransactionWithoutDoubleConsumingBalances()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"))) return;
        await using var f = await Fixture.CreateAsync();
        var setup = new LeaveService(f.Leave, new PersonnelDirectory(f.People), f.Clock);
        var policy = await setup.CreatePolicyAsync(f.Admin, "Concurrent", 16, null, null, default);
        var first = await setup.CreateRequestAsync(f.Admin, f.Self.Id, policy, new(2026,11,14), new(2026,11,14), null, default);
        var second = await setup.CreateRequestAsync(f.Admin, f.Peer.Id, policy, new(2026,11,14), new(2026,11,14), null, default);
        var gate = new ApprovalDeadlockGate();
        var options = new DbContextOptionsBuilder<LeaveDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION")!).AddInterceptors(gate).Options;
        await using var one = new LeaveDbContext(options);
        await using var two = new LeaveDbContext(options);
        var firstService = new LeaveService(one, new PersonnelDirectory(f.People), f.Clock);
        var secondService = new LeaveService(two, new PersonnelDirectory(f.People), f.Clock);
        await Task.WhenAll(firstService.DecideAsync(f.Admin, first, true, null, default),
            secondService.DecideAsync(f.Admin, second, true, null, default));
        Assert.True(gate.UpdateAttempts >= 3); // at least one whole transaction was retried
        Assert.Equal(2, await f.Leave.Requests.AsNoTracking().CountAsync(x=>x.CompanyId==f.Company.Id && x.Status=="Approved"));
        Assert.Equal(16m, await f.Leave.Balances.AsNoTracking().Where(x=>x.CompanyId==f.Company.Id).SumAsync(x=>x.UsedHours));
    }

    // Hold both real Serializable transactions immediately before their UPDATE batch.
    // Force the PK scan observed in system_health so their retained shared ranges conflict.
    private sealed class ApprovalDeadlockGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int attempts;
        public int UpdateAttempts => attempts;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken=default)
        {
            if (command.CommandText.Contains("SUM(") && command.CommandText.Contains("FROM [leave].[Requests] AS [r]"))
                command.CommandText=command.CommandText.Replace("FROM [leave].[Requests] AS [r]", "FROM [leave].[Requests] AS [r] WITH (INDEX([PK_Requests]))");
            if (command.CommandText.Contains("UPDATE [leave].[Balances]") && command.CommandText.Contains("UPDATE [leave].[Requests]"))
            {
                var current=Interlocked.Increment(ref attempts);
                if(current==2) ready.TrySetResult();
                if(current<=2) await ready.Task.WaitAsync(TimeSpan.FromSeconds(15),cancellationToken);
            }
            return result;
        }
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026,11,11,10,0,0,TimeSpan.Zero); }
    private sealed class Fixture : IAsyncDisposable
    {
        public required OrganizationDbContext Org { get; init; }
        public required PersonnelDbContext People { get; init; }
        public required LeaveDbContext Leave { get; init; }
        public required TrainingDbContext Training { get; init; }
        public required DocumentDbContext Documents { get; init; }
        public required Company Company { get; init; }
        public required Company Foreign { get; init; }
        public required Department Department { get; init; }
        public required Employee Self { get; init; }
        public required Employee Peer { get; init; }
        public required Employee Outsider { get; init; }
        public TimeProvider Clock { get; } = new FixedClock();
        public ActorScope Admin => new(71,"self-admin",Company.Id,true,true,true,true);
        public ActorScope Owner => new(72,"self-owner",Company.Id,true,false,false,false,Self.Id);
        public static async Task<Fixture> CreateAsync()
        {
            var c=Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION")!;
            if (new SqlConnectionStringBuilder(c).InitialCatalog!="HRMS_Next_Trial_20261108") throw new InvalidOperationException("Trial only.");
            var org=new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(c).Options);
            var people=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(c).Options);
            var leave=new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(c).Options);
            var training=new TrainingDbContext(new DbContextOptionsBuilder<TrainingDbContext>().UseSqlServer(c).Options);
            var documents=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlServer(c).Options);
            var company=new Company { Name="Self service "+Guid.NewGuid().ToString("N") };
            var foreign=new Company { Name="Foreign self "+Guid.NewGuid().ToString("N") };
            org.Companies.AddRange(company,foreign); await org.SaveChangesAsync();
            var d=new Department { CompanyId=company.Id,Name="Self" }; var fd=new Department { CompanyId=foreign.Id,Name="Foreign" };
            org.Departments.AddRange(d,fd); await org.SaveChangesAsync();
            Employee Person(int co,int dep)=>new() { CompanyId=co,DepartmentId=dep,FirstName="Self",LastName="Test",EmployeeNumber=Guid.NewGuid().ToString("N"),HireDate=new DateOnly(2020,1,1) };
            var self=Person(company.Id,d.Id); var peer=Person(company.Id,d.Id); var outsider=Person(foreign.Id,fd.Id);
            people.Employees.AddRange(self,peer,outsider); await people.SaveChangesAsync();
            return new() { Org=org,People=people,Leave=leave,Training=training,Documents=documents,Company=company,Foreign=foreign,Department=d,Self=self,Peer=peer,Outsider=outsider };
        }
        public async ValueTask DisposeAsync()
        {
            var ids=new[]{Company.Id,Foreign.Id};
            Leave.ChangeTracker.Clear(); People.ChangeTracker.Clear();
            await Training.Assignments.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Training.Courses.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Leave.Requests.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Leave.Balances.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Leave.Policies.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Documents.Documents.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Documents.Audits.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await People.Audits.Where(x=>x.EmployeeId==Self.Id||x.EmployeeId==Peer.Id||x.EmployeeId==Outsider.Id).ExecuteDeleteAsync();
            await People.Photos.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await People.EmployeeJobs.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await People.Employees.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Org.Departments.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Org.Companies.Where(x=>ids.Contains(x.Id)).ExecuteDeleteAsync();
            await Documents.DisposeAsync(); await Training.DisposeAsync(); await Leave.DisposeAsync(); await People.DisposeAsync(); await Org.DisposeAsync();
        }
    }
}
