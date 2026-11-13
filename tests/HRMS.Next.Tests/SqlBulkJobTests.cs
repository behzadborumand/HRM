using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace HRMS.Next.Tests;

public sealed class SqlBulkJobTests
{
    // Dropping a member, overwriting pay, missing history/audits or not bumping versions breaks this real SQL scenario.
    [Fact] public async Task GroupChangeClosesHistoryPreservesPayAndAuditsEveryMember()
    {
        if (await Fixture.Create() is not { } f) return;
        await using var fixture = f;
        var a = await f.Person("A"); var b = await f.Person("B");
        var count = await f.Service.ApplyAsync(f.Actor, f.Select(a,b), f.Change, default);
        Assert.Equal(2, count);
        f.Db.ChangeTracker.Clear();
        foreach(var employee in new[]{a,b})
        {
            var jobs = await f.Db.EmployeeJobs.AsNoTracking().Where(x=>x.EmployeeId==employee.Id).OrderBy(x=>x.StartDate).ToListAsync();
            Assert.Equal(2,jobs.Count); Assert.False(jobs[0].IsCurrent); Assert.Equal(new DateOnly(2022,12,31),jobs[0].EndDate);
            Assert.True(jobs[1].IsCurrent); Assert.Equal(new DateOnly(2023,1,1),jobs[1].StartDate);
            Assert.Equal(f.NewDepartment,jobs[1].DepartmentId); Assert.Equal("New title",jobs[1].JobTitle); Assert.Equal("PartTime",jobs[1].EmploymentType);
            Assert.Equal("Salary",jobs[1].PayType); Assert.Equal(987654321.12m,jobs[1].PayRate);
            var saved = await f.Db.Employees.AsNoTracking().SingleAsync(x=>x.Id==employee.Id);
            Assert.Equal(f.NewDepartment,saved.DepartmentId); Assert.False(saved.RowVersion.SequenceEqual(employee.RowVersion));
            var audit=Assert.Single(await f.Db.Audits.Where(x=>x.EmployeeId==employee.Id).ToListAsync());
            Assert.Equal("BulkJobChange",audit.Action); Assert.Equal(f.Actor.Id,audit.ActorUserId); Assert.DoesNotContain("987654321",audit.Summary);
        }
        var before=await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company);
        var current=await f.Db.Employees.AsNoTracking().Where(x=>x.CompanyId==f.Company).ToArrayAsync();
        Assert.Equal(0,await f.Service.ApplyAsync(f.Actor,f.Select(current),f.Change,default));
        Assert.Equal(before,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
        Assert.Equal(2,await f.Db.Audits.CountAsync(x=>x.EmployeeId==a.Id||x.EmployeeId==b.Id));
    }

    // A late-invalid member or stale token must never partially update the first valid member or leak pending tracking into a later save.
    [Fact] public async Task StaleAndLateInvalidMembersRollbackWholeGroupAndCannotReplay()
    {
        if(await Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A"); var b=await f.Person("B",start:new(2024,1,1));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,b),f.Change,default));
        await f.Db.SaveChangesAsync();
        Assert.Equal(2,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
        Assert.Empty(await f.Db.Audits.Where(x=>x.EmployeeId==a.Id||x.EmployeeId==b.Id).ToListAsync());
        Assert.All(await f.Db.Employees.Where(x=>x.CompanyId==f.Company).ToListAsync(),x=>Assert.Equal(f.Department,x.DepartmentId));
        f.Db.ChangeTracker.Clear();
        await f.Db.Employees.Where(x=>x.Id==b.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FirstName,"Changed elsewhere"));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,b),f.Change with{EffectiveDate=new(2025,1,1)},default));
        await f.Db.SaveChangesAsync();
        Assert.Equal(2,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
        Assert.Empty(await f.Db.Audits.Where(x=>x.EmployeeId==a.Id||x.EmployeeId==b.Id).ToListAsync());
        var fresh=await f.Db.Employees.AsNoTracking().Where(x=>x.CompanyId==f.Company).ToArrayAsync();
        Assert.Equal(2,await f.Service.ApplyAsync(f.Actor,f.Select(fresh),f.Change with{EffectiveDate=new(2025,1,1)},default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(fresh),f.Change with{EffectiveDate=new(2025,2,1)},default));
        Assert.Equal(4,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
    }

    [Fact] public async Task SnapshotsSelectionAndWriteGrantsAreStrictAndExcludePrivateData()
    {
        if(await Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("LOCAL"); var foreign=await f.Person("FOREIGN",foreign:true);
        f.Db.PersonalInfos.Add(new(){EmployeeId=a.Id,CompanyId=f.Company,MobilePhone="PRIVATE_BULK_PHONE"});
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var snapshot=Assert.Single(await f.Service.SelectionAsync(f.Actor,[a.Id],default));
        Assert.Equal(a.Id,snapshot.Id); Assert.Equal("Old title",snapshot.JobTitle); Assert.Equal(a.RowVersion,snapshot.Version);
        var json=JsonSerializer.Serialize(snapshot); Assert.DoesNotContain("987654321",json); Assert.DoesNotContain("PayRate",json); Assert.DoesNotContain("PRIVATE_BULK",json);
        foreach(var actor in new[]{f.Actor with{IsAdmin=false,CanWriteEmployees=false,CanReadEmployees=true},f.Actor with{IsAdmin=false,CanWriteEmployees=false,EmployeeId=a.Id},f.Actor with{IsAdmin=false,CanWriteEmployees=false,EmployeeId=a.Id,CanManageTeam=true},f.Actor with{IsActive=false},f.Actor with{CompanyId=0}})
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SelectionAsync(actor,[a.Id],default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.ApplyAsync(actor,f.Select(a),f.Change,default));
        }
        foreach(var ids in new int[][]{[],[a.Id,a.Id],[0],Enumerable.Range(1,101).ToArray()})
            await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SelectionAsync(f.Actor,ids,default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.SelectionAsync(f.Actor,[a.Id,foreign.Id],default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,foreign),f.Change,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,[new(a.Id,[])],f.Change,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,a),f.Change,default));
        Assert.Equal(1,await f.Service.ApplyAsync(f.Actor with{IsAdmin=false,CanReadEmployees=false,CanWriteEmployees=true},f.Select(a),f.Change,default));
    }

    [Fact] public async Task SelfManagerAndUnchangedFieldsPreserveHistoryAndMissingJobsNeedValidFields()
    {
        if(await Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A"); var b=await f.Person("B");
        var input=new BulkJobChangeInput(null,null,a.Id,null,new(2023,1,1));
        Assert.Equal(1,await f.Service.ApplyAsync(f.Actor,f.Select(a,b),input,default));
        f.Db.ChangeTracker.Clear();
        var first=Assert.Single(await f.Db.EmployeeJobs.Where(x=>x.EmployeeId==a.Id).ToListAsync()); Assert.Null(first.ManagerEmployeeId);
        var second=await f.Db.EmployeeJobs.SingleAsync(x=>x.EmployeeId==b.Id&&x.IsCurrent); Assert.Equal(a.Id,second.ManagerEmployeeId); Assert.Equal("Old title",second.JobTitle); Assert.Equal("FullTime",second.EmploymentType);
        f.Db.ChangeTracker.Clear();
        var noJob=await f.Person("NoJob",job:false);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(noJob),input with{ManagerEmployeeId=null,DepartmentId=f.NewDepartment},default));
        Assert.Equal(1,await f.Service.ApplyAsync(f.Actor,f.Select(noJob),f.Change,default));
        Assert.Single(await f.Db.EmployeeJobs.Where(x=>x.EmployeeId==noJob.Id&&x.IsCurrent).ToListAsync());
    }

    [Fact] public async Task DateCatalogEmploymentAndManagerValidationAreAllOrNothing()
    {
        if(await Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A"); var departed=await f.Person("Departed",departed:true); var foreign=await f.Person("Foreign",foreign:true);
        foreach(var invalid in new[]{f.Change with{EffectiveDate=new(2019,1,1)},f.Change with{EffectiveDate=new(2100,1,1)},f.Change with{EffectiveDate=new(2021,1,1)},f.Change with{JobTitle="Not in catalog"},f.Change with{DepartmentId=f.ForeignDepartment},f.Change with{EmploymentType="Forged"},f.Change with{ManagerEmployeeId=foreign.Id},f.Change with{ManagerEmployeeId=departed.Id},new BulkJobChangeInput(null,null,null,null,new(2023,1,1))})
            await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a),invalid,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,departed),f.Change,default));
        f.Db.EmployeeJobs.Add(new(){CompanyId=f.Company,EmployeeId=a.Id,StartDate=new(2024,1,1),EndDate=new(2024,2,1),JobTitle="Inconsistent future history"});
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a),f.Change,default));
        Assert.Empty(await f.Db.Audits.Where(x=>x.EmployeeId==a.Id||x.EmployeeId==departed.Id).ToListAsync());
        Assert.Equal(3,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
    }

    [Fact] public async Task EmployeeLockProtectsValidationThroughCommitAgainstConcurrentDeparture()
    {
        if(await Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var employee=await f.Person("Locked employee");
        var probe=new DepartureProbe(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION")!,employee.Id);
        await using var db=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION")).AddInterceptors(probe).Options);
        var service=new EmployeeBulkJobService(db,new OrganizationDirectory(f.org),TimeProvider.System);
        Assert.Equal(1,await service.ApplyAsync(f.Actor,f.Select(employee),f.Change,default));
        Assert.True(probe.WasBlocked,"A second SQL connection changed employment after bulk validation and before commit.");
        var saved=await f.Db.Employees.AsNoTracking().SingleAsync(x=>x.Id==employee.Id);
        Assert.Null(saved.TerminationDate);
        Assert.Equal(2,await f.Db.EmployeeJobs.CountAsync(x=>x.EmployeeId==employee.Id));
    }
    private sealed class DepartureProbe(string connection,int employeeId):DbCommandInterceptor
    {
        public bool WasBlocked {get;private set;}
        private bool triggered;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(!triggered && command.CommandText.Contains("FROM [personnel].[EmployeeJobs]"))
            {
                triggered=true;
                await using var other=new SqlConnection(connection);await other.OpenAsync(ct);
                await using var update=other.CreateCommand();
                update.CommandText="SET LOCK_TIMEOUT 600; UPDATE personnel.Employees SET TerminationDate='2026-01-01' WHERE Id=@id";
                update.Parameters.AddWithValue("@id",employeeId);
                try{await update.ExecuteNonQueryAsync(ct);}catch(SqlException error) when(error.Number==1222){WasBlocked=true;}
            }
            return result;
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public PersonnelDbContext Db {get;}
        public readonly OrganizationDbContext org;
        public int Company {get;} public int Foreign {get;} public int Department {get;} public int NewDepartment {get;} public int ForeignDepartment {get;}
        public ActorScope Actor=>new(1,"bulk-writer",Company,true,false,false,true);
        public EmployeeBulkJobService Service=>new(Db,new OrganizationDirectory(org),TimeProvider.System);
        public BulkJobChangeInput Change=>new(NewDepartment,"New title",null,"PartTime",new(2023,1,1));
        public IReadOnlyList<BulkJobSelection> Select(params Employee[] employees)=>employees.Select(x=>new BulkJobSelection(x.Id,x.RowVersion)).ToArray();
        private Fixture(PersonnelDbContext db,OrganizationDbContext org,int company,int foreign,int department,int next,int other){Db=db;this.org=org;Company=company;Foreign=foreign;Department=department;NewDepartment=next;ForeignDepartment=other;}
        public static async Task<Fixture?> Create()
        {
            var connection=Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION"); if(string.IsNullOrWhiteSpace(connection))return null;
            if(new SqlConnectionStringBuilder(connection).InitialCatalog!="HRMS_Next_Trial_20261108")throw new InvalidOperationException("Only named trial database allowed.");
            var org=new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
            var db=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
            var company=new Company{Name="BulkJob_"+Guid.NewGuid().ToString("N")}; var foreign=new Company{Name="BulkJobForeign_"+Guid.NewGuid().ToString("N")};
            org.Companies.AddRange(company,foreign);await org.SaveChangesAsync();
            var dep=new Department{CompanyId=company.Id,Name="Original"};var next=new Department{CompanyId=company.Id,Name="Next"};var other=new Department{CompanyId=foreign.Id,Name="Foreign"};
            org.Departments.AddRange(dep,next,other);org.JobTitles.Add(new(){CompanyId=company.Id,Name="New title"});await org.SaveChangesAsync();
            return new(db,org,company.Id,foreign.Id,dep.Id,next.Id,other.Id);
        }
        public async Task<Employee> Person(string name,bool foreign=false,bool departed=false,bool job=true,DateOnly? start=null)
        {
            var employee=new Employee{CompanyId=foreign?Foreign:Company,DepartmentId=foreign?ForeignDepartment:Department,EmployeeNumber=Guid.NewGuid().ToString("N"),FirstName=name,LastName="Bulk",HireDate=new(2020,1,1),TerminationDate=departed?new(2024,1,1):null};
            Db.Employees.Add(employee);await Db.SaveChangesAsync();
            if(job) {Db.EmployeeJobs.Add(new(){CompanyId=employee.CompanyId,EmployeeId=employee.Id,DepartmentId=employee.DepartmentId,JobTitle="Old title",EmploymentType="FullTime",StartDate=start??new(2021,1,1),IsCurrent=true,PayType="Salary",PayRate=987654321.12m});await Db.SaveChangesAsync();}
            Db.ChangeTracker.Clear();return employee;
        }
        public async ValueTask DisposeAsync()
        {
            Db.ChangeTracker.Clear();
            var ids=Db.Employees.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).Select(x=>x.Id);
            await Db.Audits.Where(x=>ids.Contains(x.EmployeeId)).ExecuteDeleteAsync();
            await Db.EmployeeJobs.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).ExecuteDeleteAsync();
            await Db.PersonalInfos.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).ExecuteDeleteAsync();
            await Db.Employees.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).ExecuteDeleteAsync();
            await org.JobTitles.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).ExecuteDeleteAsync();
            await org.Departments.Where(x=>x.CompanyId==Company||x.CompanyId==Foreign).ExecuteDeleteAsync();
            await org.Companies.Where(x=>x.Id==Company||x.Id==Foreign).ExecuteDeleteAsync();
            await Db.DisposeAsync();await org.DisposeAsync();
        }
    }
}