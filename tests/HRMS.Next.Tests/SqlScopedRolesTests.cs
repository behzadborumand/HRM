using System.Security.Claims;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Reporting;
using HRMS.Next.Modules.Documents;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlScopedRolesTests
{
    [Fact]
    public async Task CompanyRolesApplyOnlyTheirGrantsAndRevokeMemberCookiesWhenChangedOrArchived()
    {
        if(!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();
        var roles=new RoleAdministrationService(f.Access,TimeProvider.System);
        var roleId=await roles.SaveAsync(f.Admin,0,new("Team lead",null,false,false,true,true),null,default);
        Assert.True(roleId>0);
        var accounts=new AccountAdministrationService(f.Access,new PersonnelDirectory(f.People),new PasswordHasher<AppUser>(),TimeProvider.System);
        var name="role_"+Guid.NewGuid().ToString("N")[..12];
        var userId=await accounts.CreateAsync(f.Admin,new(name,"RolePassword123!",f.Manager.Id,false,false,roleId),default);
        var stored=await f.Access.Users.AsNoTracking().SingleAsync(x=>x.Id==userId);
        Assert.Equal(roleId,stored.RoleId);
        var auth=new AccessService(f.Access,new PasswordHasher<AppUser>());
        var actor=await auth.ValidateAsync(name,"RolePassword123!",default);
        Assert.NotNull(actor);
        Assert.True(actor.CanManageTeam);
        Assert.False(AccessRules.CanRead(actor,f.Company.Id));
        Assert.False(actor.IsAdmin);
        Assert.NotNull(await auth.GetActiveAsync(Principal(userId,stored.RowVersion),default));
        var role=Assert.Single((await roles.ListAsync(f.Admin,1,default)).Rows);
        Assert.Equal(1,role.UserCount);
        await roles.SaveAsync(f.Admin,roleId,new("Team reader",null,true,false,true,true),role.RowVersion,default);
        Assert.Null(await auth.GetActiveAsync(Principal(userId,stored.RowVersion),default));
        Assert.True(AccessRules.CanRead((await auth.ValidateAsync(name,"RolePassword123!",default))!,f.Company.Id));
        var secondUser=await f.Access.Users.AsNoTracking().SingleAsync(x=>x.Id==userId);
        role=Assert.Single((await roles.ListAsync(f.Admin,1,default)).Rows);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>roles.SaveAsync(f.Admin,roleId,new("Stale",null,false,false,false,true),[0],default));
        await roles.SaveAsync(f.Admin,roleId,new("Archived",null,true,false,true,false),role.RowVersion,default);
        Assert.Null(await auth.GetActiveAsync(Principal(userId,secondUser.RowVersion),default));
        actor=await auth.ValidateAsync(name,"RolePassword123!",default);
        Assert.False(actor!.CanManageTeam);
        Assert.False(AccessRules.CanRead(actor,f.Company.Id));
        var current=await f.Access.Users.AsNoTracking().SingleAsync(x=>x.Id==userId);
        await accounts.UpdateAsync(f.Admin,userId,new(f.Manager.Id,true,false,true,roleId),current.RowVersion,default);
        Assert.True(AccessRules.CanRead((await auth.ValidateAsync(name,"RolePassword123!",default))!,f.Company.Id)); // explicit grant retained
        var foreignRole=await roles.SaveAsync(f.Admin with{CompanyId=f.Foreign.Id},0,new("Foreign",null,true,false,false,true),null,default);
        await Assert.ThrowsAsync<ArgumentException>(()=>accounts.CreateAsync(f.Admin,new("bad_"+Guid.NewGuid().ToString("N")[..12],"RolePassword123!",null,false,false,foreignRole),default));
        var teamRole=await roles.SaveAsync(f.Admin,0,new("Active manager",null,false,false,true,true),null,default);
        await Assert.ThrowsAsync<ArgumentException>(()=>accounts.CreateAsync(f.Admin,new("bad_"+Guid.NewGuid().ToString("N")[..12],"RolePassword123!",null,false,false,teamRole),default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>roles.ListAsync(f.TeamActor,1,default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>roles.SaveAsync(f.Admin,foreignRole,new("Hack",null,true,true,true,true),[0],default));
        Assert.Equal(3,await f.Access.RoleAudits.CountAsync(x=>x.CompanyId==f.Company.Id&&x.RoleId==roleId));
        var writer=await roles.SaveAsync(f.Admin,0,new("Writer",null,false,true,false,true),null,default);
        Assert.True((await roles.ChoicesAsync(f.Admin,default)).Single(x=>x.Id==writer).CanReadEmployees);
    }

    [Fact]
    public async Task ExplicitManagerGrantReadsOnlyCurrentActiveDirectReportsAndNeverTheirPayOrFiles()
    {
        if(!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();
        var people=new EmployeeService(f.People,new OrganizationDirectory(f.Org),TimeProvider.System);
        Assert.Equal(f.Report.Id,(await people.GetAsync(f.TeamActor,f.Report.Id,default))!.Id);
        var teams=new PersonnelTeamService(f.People);
        Assert.Equal(f.Report.Id,Assert.Single((await teams.ListAsync(f.TeamActor,null,1,default)).Rows).Id);
        foreach(var target in new[]{f.Peer.Id,f.Indirect.Id,f.Outsider.Id})
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.GetAsync(f.TeamActor,target,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.GetAsync(f.TeamActor with{CanManageTeam=false},f.Report.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.GetAsync(f.TeamActor with{IsActive=false},f.Report.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.SearchAsync(f.TeamActor,null,null,1,20,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new EmployeePayService(f.People).HistoryAsync(f.TeamActor,f.Report.Id,default));
        await using var docs=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlServer(Fixture.Connection).Options);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new DocumentService(docs,new PersonnelDirectory(f.People),TimeProvider.System).ListAsync(f.TeamActor,f.Report.Id,1,20,default));
        Assert.Single(await new EmployeeJobService(f.People,new OrganizationDirectory(f.Org),TimeProvider.System).HistoryAsync(f.TeamActor,f.Report.Id,default));
        f.ReportJob.ManagerEmployeeId=f.Peer.Id; await f.People.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.GetAsync(f.TeamActor,f.Report.Id,default));
        Assert.Empty((await teams.ListAsync(f.TeamActor,null,1,default)).Rows);
        f.ReportJob.ManagerEmployeeId=f.Manager.Id; f.Report.TerminationDate=new DateOnly(2026,1,1); await f.People.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>people.GetAsync(f.TeamActor,f.Report.Id,default));
    }

    [Fact]
    public async Task TeamQueueAndDecisionsRecheckRelationshipRejectSelfAndRequireCurrentRequestVersion()
    {
        if(!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();
        var leave=new LeaveService(f.Leave,new PersonnelDirectory(f.People),TimeProvider.System);
        var policy=await leave.CreatePolicyAsync(f.Admin,"Annual",240,null,null,default);
        var id=await leave.CreateRequestAsync(f.Admin,f.Report.Id,policy,new(2026,11,14),new(2026,11,14),"PRIVATE_REASON",default);
        var peer=await leave.CreateRequestAsync(f.Admin,f.Peer.Id,policy,new(2026,11,14),new(2026,11,14),null,default);
        var self=await leave.CreateRequestAsync(f.Admin,f.Manager.Id,policy,new(2026,11,14),new(2026,11,14),null,default);
        var board=new TeamLeaveBoard(f.Leave);
        var page=await board.LoadAsync(f.TeamActor,1,default);
        Assert.Equal(id,Assert.Single(page.Rows).Id);
        var version=page.Rows[0].RowVersion;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>leave.DecideAsync(f.TeamActor,id,true,null,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>board.LoadAsync(f.TeamActor with { EmployeeId=null },1,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>leave.DecideAsync(f.TeamActor,self,true,null,default,version));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>leave.DecideAsync(f.TeamActor,peer,true,null,default,version));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>leave.DecideAsync(f.TeamActor with{CanManageTeam=false},id,true,null,default,version));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>leave.DecideAsync(f.TeamActor,id,true,null,default,[0]));
        f.ReportJob.ManagerEmployeeId=f.Peer.Id; await f.People.SaveChangesAsync();
        Assert.Empty((await board.LoadAsync(f.TeamActor,1,default)).Rows);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>leave.DecideAsync(f.TeamActor,id,true,null,default,version));
        f.ReportJob.ManagerEmployeeId=f.Manager.Id; await f.People.SaveChangesAsync();
        await leave.DecideAsync(f.TeamActor,id,true,"Team approved",default,version);
        f.Leave.ChangeTracker.Clear();
        var request=await f.Leave.Requests.SingleAsync(x=>x.Id==id);
        Assert.Equal("Approved",request.Status);
        Assert.Equal(f.TeamActor.Id,request.DecidedByUserId);
        Assert.Equal(8m,(await f.Leave.Balances.SingleAsync(x=>x.EmployeeId==f.Report.Id&&x.CompanyId==f.Company.Id)).UsedHours);
        await Assert.ThrowsAsync<ArgumentException>(()=>leave.DecideAsync(f.TeamActor,id,true,null,default,request.RowVersion));
    }

    [Fact]
    public async Task TeamPagingCountsOnlyActiveDirectReportsAndStopsWhenManagerDeparts()
    {
        if(!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();
        var policy=await new LeaveService(f.Leave,new PersonnelDirectory(f.People),TimeProvider.System)
            .CreatePolicyAsync(f.Admin,"Paging",0,null,null,default);
        for(var i=0;i<22;i++)
        {
            var employee=new Employee {CompanyId=f.Company.Id,DepartmentId=f.Report.DepartmentId,
                EmployeeNumber="PAGE"+i,FirstName="Paged",LastName=i.ToString("D2"),HireDate=new(2020,1,1)};
            f.People.Employees.Add(employee);await f.People.SaveChangesAsync();
            f.People.EmployeeJobs.Add(new EmployeeJob {CompanyId=f.Company.Id,EmployeeId=employee.Id,
                DepartmentId=employee.DepartmentId,ManagerEmployeeId=f.Manager.Id,IsCurrent=true,StartDate=employee.HireDate});
            f.Leave.Requests.Add(new LeaveRequest {CompanyId=f.Company.Id,EmployeeId=employee.Id,PolicyId=policy,
                StartDate=new(2026,12,1),EndDate=new(2026,12,1),Hours=8,Status="Pending",Reason="PRIVATE_PAGING",RequestedAtUtc=DateTimeOffset.UtcNow});
        }
        await f.People.SaveChangesAsync();await f.Leave.SaveChangesAsync();
        // A same-company peer and a foreign employee that references this manager must not affect either page or count.
        foreach(var employee in new[]{f.Peer,f.Outsider})
        {
            var foreignPolicy=employee.CompanyId==f.Company.Id ? policy : await new LeaveService(f.Leave,new PersonnelDirectory(f.People),TimeProvider.System)
                .CreatePolicyAsync(f.Admin with {CompanyId=f.Foreign.Id},"Foreign paging",0,null,null,default);
            f.Leave.Requests.Add(new LeaveRequest {CompanyId=employee.CompanyId,EmployeeId=employee.Id,PolicyId=foreignPolicy,
                StartDate=new(2026,12,1),EndDate=new(2026,12,1),Hours=8,Status="Pending",RequestedAtUtc=DateTimeOffset.UtcNow});
        }
        await f.Leave.SaveChangesAsync();
        var teams=new PersonnelTeamService(f.People);
        var first=await teams.ListAsync(f.TeamActor,"Paged",1,default);
        var last=await teams.ListAsync(f.TeamActor,"Paged",99,default);
        Assert.Equal(22,first.Total);Assert.Equal(20,first.Rows.Count);Assert.Equal(2,last.Page);Assert.Equal(2,last.Rows.Count);
        Assert.Empty(first.Rows.Select(x=>x.Id).Intersect(last.Rows.Select(x=>x.Id)));
        var board=new TeamLeaveBoard(f.Leave);
        var queue=await board.LoadAsync(f.TeamActor,1,default);var queueLast=await board.LoadAsync(f.TeamActor,99,default);
        Assert.Equal(22,queue.Total);Assert.Equal(20,queue.Rows.Count);Assert.Equal(2,queueLast.Rows.Count);
        Assert.Empty(queue.Rows.Select(x=>x.Id).Intersect(queueLast.Rows.Select(x=>x.Id)));
        Assert.Null(await new EmployeePhotoService(f.People,TimeProvider.System).ReadAsync(f.TeamActor,f.Report.Id,default));
        f.Manager.TerminationDate=new(2026,11,1);await f.People.SaveChangesAsync();
        Assert.Empty((await teams.ListAsync(f.TeamActor,null,1,default)).Rows);
        Assert.Empty((await board.LoadAsync(f.TeamActor,1,default)).Rows);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new EmployeePhotoService(f.People,TimeProvider.System).ReadAsync(f.TeamActor,f.Report.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new LeaveService(f.Leave,new PersonnelDirectory(f.People),TimeProvider.System)
            .DecideAsync(f.TeamActor,queue.Rows[0].Id,true,null,default,queue.Rows[0].RowVersion));
    }
    private static ClaimsPrincipal Principal(int id,byte[] version)=>new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(AccessService.AuthVersionClaim,Convert.ToBase64String(version))],"Cookies"));

    private sealed class Fixture : IAsyncDisposable
    {
        public static string? Connection=>Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        public static bool Enabled=>!string.IsNullOrWhiteSpace(Connection);
        public required OrganizationDbContext Org {get;init;}
        public required PersonnelDbContext People {get;init;}
        public required AccessDbContext Access {get;init;}
        public required LeaveDbContext Leave {get;init;}
        public required Company Company {get;init;}
        public required Company Foreign {get;init;}
        public required Employee Manager {get;init;}
        public required Employee Report {get;init;}
        public required Employee Peer {get;init;}
        public required Employee Indirect {get;init;}
        public required Employee Outsider {get;init;}
        public required EmployeeJob ReportJob {get;init;}
        public ActorScope Admin=>new(901,"role-admin",Company.Id,true,true,true,true);
        public ActorScope TeamActor=>new(902,"manager",Company.Id,true,false,false,false,Manager.Id,CanManageTeam:true);
        public static async Task<Fixture> CreateAsync()
        {
            if(new SqlConnectionStringBuilder(Connection!).InitialCatalog!="HRMS_Next_Trial_20261108")throw new InvalidOperationException("Trial only.");
            var org=new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(Connection!).Options);
            var people=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(Connection!).Options);
            var access=new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseSqlServer(Connection!).Options);
            var leave=new LeaveDbContext(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlServer(Connection!).Options);
            var company=new Company{Name="Scoped roles "+Guid.NewGuid().ToString("N")};var foreign=new Company{Name="Foreign scoped "+Guid.NewGuid().ToString("N")};
            org.Companies.AddRange(company,foreign);await org.SaveChangesAsync();
            var department=new Department{CompanyId=company.Id,Name="Team"};var other=new Department{CompanyId=foreign.Id,Name="Other"};
            org.Departments.AddRange(department,other);await org.SaveChangesAsync();
            Employee Person(int c,int d,string n)=>new(){CompanyId=c,DepartmentId=d,EmployeeNumber=Guid.NewGuid().ToString("N"),FirstName=n,LastName="Team",HireDate=new DateOnly(2020,1,1)};
            var manager=Person(company.Id,department.Id,"Manager");var report=Person(company.Id,department.Id,"Report");var peer=Person(company.Id,department.Id,"Peer");var indirect=Person(company.Id,department.Id,"Indirect");var outsider=Person(foreign.Id,other.Id,"Foreign");
            people.Employees.AddRange(manager,report,peer,indirect,outsider);await people.SaveChangesAsync();
            EmployeeJob Job(Employee p,int? m)=>new(){CompanyId=p.CompanyId,EmployeeId=p.Id,DepartmentId=p.DepartmentId,ManagerEmployeeId=m,StartDate=p.HireDate,IsCurrent=true,JobTitle="Analyst",EmploymentType="FullTime"};
            var reportJob=Job(report,manager.Id);people.EmployeeJobs.AddRange(reportJob,Job(peer,null),Job(indirect,report.Id),Job(outsider,manager.Id));await people.SaveChangesAsync();
            return new(){Org=org,People=people,Access=access,Leave=leave,Company=company,Foreign=foreign,Manager=manager,Report=report,Peer=peer,Indirect=indirect,Outsider=outsider,ReportJob=reportJob};
        }
        public async ValueTask DisposeAsync()
        {
            var ids=new[]{Company.Id,Foreign.Id};Access.ChangeTracker.Clear();People.ChangeTracker.Clear();Leave.ChangeTracker.Clear();
            await Leave.Requests.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Leave.Balances.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Leave.Policies.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Access.Audits.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Access.Users.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Access.RoleAudits.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Access.Roles.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await People.EmployeeJobs.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await People.Employees.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Org.Departments.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Org.Companies.Where(x=>ids.Contains(x.Id)).ExecuteDeleteAsync();
            await Leave.DisposeAsync();await Access.DisposeAsync();await People.DisposeAsync();await Org.DisposeAsync();
        }
    }
}
