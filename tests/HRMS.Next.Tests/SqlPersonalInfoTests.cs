using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
namespace HRMS.Next.Tests;

public sealed class SqlPersonalInfoTests
{
    private static PersonalContactInput Contact => new("Private preferred","021-12345678","۰۹۰۰۱۲۳۴۵۶۷",
        "PRIVATE_ADDRESS","Unit 2","PRIVATE_CITY","Province","۱۲۳۴۵۶۷۸۹۰","Iran","PRIVATE_EMERGENCY","+98 900 9999999","Sibling");

    [Fact]
    public async Task OwnerContactSaveNormalizesDigitsAuditsNamesOnlyAndPrivateReadDeniesCompanyGrants()
    {
        if (!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();
        var service=new EmployeePersonalService(f.People,TimeProvider.System);
        await service.SaveContactAsync(f.Owner,f.Employee.Id,Contact,null,default);
        var info=await service.ReadAsync(f.Owner,f.Employee.Id,default);
        Assert.NotNull(info);Assert.Equal("09001234567",info.MobilePhone);Assert.Equal("1234567890",info.PostalCode);
        Assert.Equal("PRIVATE_ADDRESS",info.AddressLine1);Assert.Equal("Unit 2",info.AddressLine2);
        Assert.Equal("PRIVATE_EMERGENCY",info.EmergencyContactName);
        foreach(var actor in new[]{f.Owner with {EmployeeId=null,CanReadEmployees=true},f.Owner with {EmployeeId=null,CanWriteEmployees=true},
            f.Owner with {EmployeeId=f.Peer.Id,CanManageTeam=true},f.Owner with {IsActive=false}})
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ReadAsync(actor,f.Employee.Id,default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveContactAsync(actor,f.Employee.Id,Contact,info.RowVersion,default));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveContactAsync(f.Owner,f.Peer.Id,Contact,null,default));
        Assert.NotNull(await service.ReadAsync(f.Admin,f.Employee.Id,default));
        Assert.Null(await service.ReadAsync(f.Admin with {CompanyId=f.Foreign.Id},f.Employee.Id,default));
        var audit=Assert.Single(await f.People.Audits.AsNoTracking().Where(x=>x.EmployeeId==f.Employee.Id).ToListAsync());
        Assert.Equal(f.Owner.Id,audit.ActorUserId);Assert.Equal("PersonalContact",audit.Action);
        Assert.Contains("MobilePhone",audit.Summary);
        Assert.DoesNotContain("PRIVATE_",audit.Summary);Assert.DoesNotContain("09001234567",audit.Summary);
    }

    [Fact]
    public async Task OnlyAdminChangesIdentityAndOwnerContactUpdatePreservesIt()
    {
        if (!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();var service=new EmployeePersonalService(f.People,TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveIdentityAsync(f.Owner,f.Employee.Id,new("+98 21 1234",new(1990,1,2),"Female","Single"),null,default));
        await service.SaveIdentityAsync(f.Admin,f.Employee.Id,new("+98 21 1234",new(1990,1,2),"Female","Single"),null,default);
        var before=(await service.ReadAsync(f.Owner,f.Employee.Id,default))!;Assert.NotNull(before);
        await service.SaveContactAsync(f.Owner,f.Employee.Id,Contact,before.RowVersion,default);
        var after=(await service.ReadAsync(f.Owner,f.Employee.Id,default))!;
        Assert.Equal(new DateOnly(1990,1,2),after.DateOfBirth);Assert.Equal("Female",after.Gender);Assert.Equal("Single",after.MaritalStatus);Assert.Equal("+98 21 1234",after.Phone);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveIdentityAsync(f.Admin with {CompanyId=f.Foreign.Id},f.Employee.Id,new(null,null,null,null),after.RowVersion,default));
        f.Employee.TerminationDate=new(2026,1,1);await f.People.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveContactAsync(f.Owner,f.Employee.Id,Contact,after.RowVersion,default));
        Assert.NotNull(await service.ReadAsync(f.Owner,f.Employee.Id,default));
        await service.SaveContactAsync(f.Admin,f.Employee.Id,Contact with {City="Archived edit"},after.RowVersion,default);
    }

    [Fact]
    public async Task PersonalEditsRejectStaleOrMissingVersionsAndPreserveWinningData()
    {
        if (!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();var service=new EmployeePersonalService(f.People,TimeProvider.System);
        await service.SaveContactAsync(f.Owner,f.Employee.Id,Contact,null,default);
        var initial=(await service.ReadAsync(f.Owner,f.Employee.Id,default))!;Assert.NotNull(initial);
        await using var second=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(Fixture.Connection).Options);
        var other=new EmployeePersonalService(second,TimeProvider.System);
        await other.SaveContactAsync(f.Owner,f.Employee.Id,Contact with {City="Winner"},initial.RowVersion,default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>service.SaveContactAsync(f.Owner,f.Employee.Id,Contact with {City="Loser"},initial.RowVersion,default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>other.SaveContactAsync(f.Owner,f.Employee.Id,Contact,null,default));
        Assert.Equal("Winner",(await other.ReadAsync(f.Owner,f.Employee.Id,default))!.City);
        Assert.Equal(2,await second.Audits.CountAsync(x=>x.EmployeeId==f.Employee.Id));
    }

    [Fact]
    public async Task InvalidPersonalValuesDoNotCreateRowsAndDatabaseEnforcesCompanyEmployeePair()
    {
        if (!Fixture.Enabled) return;
        await using var f=await Fixture.CreateAsync();var service=new EmployeePersonalService(f.People,TimeProvider.System);
        foreach(var input in new[]{Contact with {City=new string('x',101)},Contact with {MobilePhone="not a phone"},Contact with {PreferredName="bad\0name"}})
            await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveContactAsync(f.Owner,f.Employee.Id,input,null,default));
        foreach(var identity in new[]{new PersonalIdentityInput(null,new(2030,1,1),null,null),new(null,new(2025,1,1),null,null),new(null,new(1,1,1),null,null),new(null,null,new string('x',21),null)})
            await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveIdentityAsync(f.Admin,f.Employee.Id,identity,null,default));
        Assert.Equal(0,await f.People.PersonalInfos.CountAsync(x=>x.CompanyId==f.Company.Id));
        f.People.PersonalInfos.Add(new EmployeePersonalInfo{EmployeeId=f.Employee.Id,CompanyId=f.Foreign.Id});
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.People.SaveChangesAsync());
        f.People.ChangeTracker.Clear();
    }

    [Fact]
    public async Task EmployeeStateCannotChangeBetweenAuthorizationAndPersonalWrite()
    {
        if(!Fixture.Enabled)return;
        await using var f=await Fixture.CreateAsync();
        var original=new EmployeePersonalService(f.People,TimeProvider.System);
        await original.SaveContactAsync(f.Owner,f.Employee.Id,Contact,null,default);
        var version=(await original.ReadAsync(f.Owner,f.Employee.Id,default))!.RowVersion;
        var probe=new DepartureProbe(Fixture.Connection!,f.Employee.Id,f.Company.Id);
        await using var db=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(Fixture.Connection).AddInterceptors(probe).Options);
        var service=new EmployeePersonalService(db,TimeProvider.System);
        await service.SaveContactAsync(f.Owner,f.Employee.Id,Contact with{City="Protected save"},version,default);
        Assert.True(probe.WasBlocked,"Termination committed after authorization but before the personal write.");
        f.Employee.TerminationDate=new(2026,1,1);await f.People.SaveChangesAsync();
        var current=(await service.ReadAsync(f.Owner,f.Employee.Id,default))!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveContactAsync(f.Owner,f.Employee.Id,Contact,current.RowVersion,default));
    }

    private sealed class DepartureProbe(string connection,int employeeId,int companyId):DbCommandInterceptor
    {
        public bool WasBlocked{get;private set;}
        private bool triggered;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(!triggered && command.CommandText.Contains("UPDATE [personnel].[PersonalInfos]"))
            {
                triggered=true;
                await using var other=new SqlConnection(connection);await other.OpenAsync(ct);
                await using var update=other.CreateCommand();
                update.CommandText="SET LOCK_TIMEOUT 600; UPDATE personnel.Employees SET TerminationDate='2026-01-01' WHERE Id=@id AND CompanyId=@company";
                update.Parameters.AddWithValue("@id",employeeId);update.Parameters.AddWithValue("@company",companyId);
                try{await update.ExecuteNonQueryAsync(ct);}
                catch(SqlException error)when(error.Number==1222){WasBlocked=true;}
            }
            return result;
        }
    }
    [Fact]
    public async Task FailedDatabaseSaveDoesNotLeaveAnAuditForTheNextSuccessfulEdit()
    {
        if(!Fixture.Enabled)return;
        await using var f=await Fixture.CreateAsync();
        var original=new EmployeePersonalService(f.People,TimeProvider.System);
        await original.SaveContactAsync(f.Owner,f.Employee.Id,Contact,null,default);
        var version=(await original.ReadAsync(f.Owner,f.Employee.Id,default))!.RowVersion;
        await using var db=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(Fixture.Connection)
            .AddInterceptors(new PersonalVersionRace(Fixture.Connection!,f.Employee.Id)).Options);
        var service=new EmployeePersonalService(db,TimeProvider.System);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>service.SaveContactAsync(f.Owner,f.Employee.Id,Contact with{City="Failed edit"},version,default));
        var winner=(await service.ReadAsync(f.Owner,f.Employee.Id,default))!;
        Assert.Equal("External winner",winner.City);
        await service.SaveContactAsync(f.Owner,f.Employee.Id,Contact with{City="Successful retry"},winner.RowVersion,default);
        Assert.Equal(2,await db.Audits.CountAsync(x=>x.EmployeeId==f.Employee.Id));
    }

    private sealed class PersonalVersionRace(string connection,int employeeId):DbCommandInterceptor
    {
        private bool triggered;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(!triggered && command.CommandText.Contains("UPDATE [personnel].[PersonalInfos]"))
            {
                triggered=true;await using var other=new SqlConnection(connection);await other.OpenAsync(ct);
                await using var update=other.CreateCommand();
                update.CommandText="UPDATE personnel.PersonalInfos SET City=N'External winner' WHERE EmployeeId=@id";
                update.Parameters.AddWithValue("@id",employeeId);await update.ExecuteNonQueryAsync(ct);
            }
            return result;
        }
    }

    [Fact]
    public async Task EmploymentDateEditCannotMoveHireBeforePrivateBirthDate()
    {
        if(!Fixture.Enabled)return;
        await using var f=await Fixture.CreateAsync();
        await new EmployeePersonalService(f.People,TimeProvider.System).SaveIdentityAsync(f.Admin,f.Employee.Id,new(null,new(1990,1,1),null,null),null,default);
        var service=new EmployeeService(f.People,new OrganizationDirectory(f.Org),TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateAsync(f.Admin,f.Employee.Id,
            new(f.Employee.DepartmentId,f.Employee.EmployeeNumber,f.Employee.FirstName,f.Employee.LastName,null,new(1980,1,1)),f.Employee.RowVersion,default));
    }

    private sealed class Fixture:IAsyncDisposable
    {
        public static string? Connection=>Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        public static bool Enabled=>!string.IsNullOrWhiteSpace(Connection);
        public required OrganizationDbContext Org{get;init;}
        public required PersonnelDbContext People{get;init;}
        public required Company Company{get;init;}
        public required Company Foreign{get;init;}
        public required Employee Employee{get;init;}
        public required Employee Peer{get;init;}
        public ActorScope Owner=>new(911,"personal-owner",Company.Id,true,false,false,false,Employee.Id);
        public ActorScope Admin=>new(912,"personal-admin",Company.Id,true,true,true,true);
        public static async Task<Fixture> CreateAsync()
        {
            if(new SqlConnectionStringBuilder(Connection!).InitialCatalog!="HRMS_Next_Trial_20261108")throw new InvalidOperationException("Trial only");
            var org=new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(Connection!).Options);
            var people=new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(Connection!).Options);
            var company=new Company{Name="Personal private "+Guid.NewGuid().ToString("N")};var foreign=new Company{Name="Foreign personal "+Guid.NewGuid().ToString("N")};
            org.Companies.AddRange(company,foreign);await org.SaveChangesAsync();
            var d=new Department{CompanyId=company.Id,Name="Personal"};org.Departments.Add(d);await org.SaveChangesAsync();
            Employee Person(string name)=>new(){CompanyId=company.Id,DepartmentId=d.Id,EmployeeNumber=Guid.NewGuid().ToString("N"),FirstName=name,LastName="Private",HireDate=new(2020,1,1)};
            var employee=Person("Owner");var peer=Person("Peer");people.Employees.AddRange(employee,peer);await people.SaveChangesAsync();
            return new(){Org=org,People=people,Company=company,Foreign=foreign,Employee=employee,Peer=peer};
        }
        public async ValueTask DisposeAsync()
        {
            People.ChangeTracker.Clear();var ids=new[]{Company.Id,Foreign.Id};
            await People.PersonalInfos.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await People.Audits.Where(x=>x.EmployeeId==Employee.Id||x.EmployeeId==Peer.Id).ExecuteDeleteAsync();
            await People.Employees.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await Org.Departments.Where(x=>ids.Contains(x.CompanyId)).ExecuteDeleteAsync();await Org.Companies.Where(x=>ids.Contains(x.Id)).ExecuteDeleteAsync();
            await People.DisposeAsync();await Org.DisposeAsync();
        }
    }
}
