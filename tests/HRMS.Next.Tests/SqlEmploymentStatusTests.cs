using System.Text.Json;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace HRMS.Next.Tests;

public sealed class SqlEmploymentStatusTests
{
    [Fact] public async Task StatusOnlyUpdatesWorkWithoutJobAndPreservePayHistoryAndVersions()
    {
        if(await SqlBulkJobTests.Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A");var b=await f.Person("No job",job:false);
        var input=new BulkJobChangeInput(null,null,null,null,new(2023,1,1)){EmploymentStatus="OnLeave"};
        Assert.Equal(2,await f.Service.ApplyAsync(f.Actor,f.Select(a,b),input,default));
        f.Db.ChangeTracker.Clear();
        var employees=await f.Db.Employees.AsNoTracking().Where(x=>x.CompanyId==f.Company).ToArrayAsync();
        Assert.All(employees,x=>Assert.True(x.IsOnLeave));
        Assert.False(employees.Single(x=>x.Id==a.Id).RowVersion.SequenceEqual(a.RowVersion));
        var jobs=await f.Db.EmployeeJobs.Where(x=>x.CompanyId==f.Company).ToListAsync();
        var job=Assert.Single(jobs);Assert.Equal(987654321.12m,job.PayRate);Assert.True(job.IsCurrent);Assert.Equal(new DateOnly(2021,1,1),job.StartDate);
        var service=new EmployeeEmploymentService(f.Db);
        var row=Assert.Single(await service.HistoryAsync(f.Actor,a.Id,default));
        Assert.Equal("Active",row.FromStatus);Assert.Equal("OnLeave",row.ToStatus);Assert.Equal(new DateOnly(2023,1,1),row.EffectiveDate);
        Assert.DoesNotContain("Pay",JsonSerializer.Serialize(row));
        Assert.Equal(2,await f.Db.Audits.CountAsync(x=>x.EmployeeId==a.Id||x.EmployeeId==b.Id));
        Assert.Equal(0,await f.Service.ApplyAsync(f.Actor,f.Select(employees),input,default));
        Assert.Single(await service.HistoryAsync(f.Actor,a.Id,default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,b),input with{EmploymentStatus="Active"},default));
        Assert.Equal(2,await f.Service.ApplyAsync(f.Actor,f.Select(employees),input with{EmploymentStatus="Active"},default));
        var history=await service.HistoryAsync(f.Actor,a.Id,default);Assert.Equal(2,history.Count);Assert.Equal("Active",history[0].ToStatus);
        Assert.Equal(1,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
    }
    [Fact] public async Task MixedJobStatusChangesRollbackWhenAnyMemberIsInvalidAndDatesCannotBacktrack()
    {
        if(await SqlBulkJobTests.Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A");var b=await f.Person("B",start:new(2024,1,1));
        var input=f.Change with{EmploymentStatus="OnLeave"};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(a,b),input,default));
        await f.Db.SaveChangesAsync();Assert.False((await f.Db.Employees.AsNoTracking().SingleAsync(x=>x.Id==a.Id)).IsOnLeave);
        Assert.Empty(await new EmployeeEmploymentService(f.Db).HistoryAsync(f.Actor,a.Id,default));
        Assert.Empty(await f.Db.Audits.Where(x=>x.EmployeeId==a.Id||x.EmployeeId==b.Id).ToListAsync());
        Assert.Equal(2,await f.Service.ApplyAsync(f.Actor,f.Select(a,b),input with{EffectiveDate=new(2025,1,1)},default));
        f.Db.ChangeTracker.Clear();var fresh=await f.Db.Employees.AsNoTracking().Where(x=>x.CompanyId==f.Company).ToArrayAsync();
        var status=new BulkJobChangeInput(null,null,null,null,new(2024,12,31)){EmploymentStatus="Active"};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(fresh),status,default));
        foreach(var invalid in new[]{status with{EmploymentStatus="Terminated"},status with{EmploymentStatus="Forged"},status with{EffectiveDate=new(2100,1,1)},status with{EffectiveDate=new(2019,1,1)}})
            await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(fresh),invalid,default));
        Assert.All(await f.Db.Employees.AsNoTracking().Where(x=>x.CompanyId==f.Company).ToListAsync(),x=>Assert.True(x.IsOnLeave));
        Assert.Equal(4,await f.Db.EmployeeJobs.CountAsync(x=>x.CompanyId==f.Company));
    }
    [Fact] public async Task StatusDirectoryFiltersAndHistoryStayTenantScopedWithoutRevokingOwnerAccess()
    {
        if(await SqlBulkJobTests.Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A");var b=await f.Person("B");var foreign=await f.Person("Foreign",foreign:true);var departed=await f.Person("Departed",departed:true);
        var input=new BulkJobChangeInput(null,null,null,null,new(2023,1,1)){EmploymentStatus="OnLeave"};
        Assert.Equal(1,await f.Service.ApplyAsync(f.Actor,f.Select(a),input,default));
        var service=new EmployeeService(f.Db,new HRMS.Next.Modules.Organization.OrganizationDirectory(f.org),TimeProvider.System);
        var onLeave=await service.SearchAsync(f.Actor,null,null,1,20,default,"OnLeave");Assert.Equal(a.Id,Assert.Single(onLeave.Rows).Id);
        var active=await service.SearchAsync(f.Actor,null,null,1,20,default,"Active");Assert.Equal(b.Id,Assert.Single(active.Rows).Id);
        Assert.Equal(3,(await service.SearchAsync(f.Actor,null,null,1,20,default)).Total);
        var owner=f.Actor with{CanWriteEmployees=false,CanReadEmployees=false,EmployeeId=a.Id};
        Assert.NotNull(await service.GetAsync(owner,a.Id,default));
        var history=new EmployeeEmploymentService(f.Db);Assert.Single(await history.HistoryAsync(owner,a.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>history.HistoryAsync(owner,b.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.ApplyAsync(owner,f.Select(a),input,default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(b,foreign),input,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyAsync(f.Actor,f.Select(b,departed),input,default));
        Assert.False((await f.Db.Employees.AsNoTracking().SingleAsync(x=>x.Id==b.Id)).IsOnLeave);
    }
    [Fact] public async Task EmploymentDateEditCannotMoveHireAfterDatedStatusHistory()
    {
        if(await SqlBulkJobTests.Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("NoJob",job:false);
        await f.Service.ApplyAsync(f.Actor,f.Select(a),new(null,null,null,null,new(2023,1,1)){EmploymentStatus="OnLeave"},default);
        f.Db.ChangeTracker.Clear();
        var current=await f.Db.Employees.AsNoTracking().SingleAsync(x=>x.Id==a.Id);
        var employees=new EmployeeService(f.Db,new HRMS.Next.Modules.Organization.OrganizationDirectory(f.org),TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>employees.UpdateAsync(f.Actor,a.Id,new(a.DepartmentId,a.EmployeeNumber,a.FirstName,a.LastName,null,new(2024,1,1)),current.RowVersion,default));
    }
    [Fact] public async Task StatusHistoryEnforcesCompanyPairAndReturnsOnlyTheLatestHundredSafeRows()
    {
        if(await SqlBulkJobTests.Fixture.Create() is not {} f)return;
        await using var fixture=f;
        var a=await f.Person("A");
        f.Db.EmploymentStatusChanges.Add(new(){CompanyId=f.Foreign,EmployeeId=a.Id,EffectiveDate=new(2025,1,1),FromStatus="Active",ToStatus="OnLeave",ActorUserId=1,AtUtc=DateTimeOffset.UtcNow});
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());f.Db.ChangeTracker.Clear();
        for(var i=0;i<101;i++) f.Db.EmploymentStatusChanges.Add(new(){CompanyId=f.Company,EmployeeId=a.Id,EffectiveDate=new(2025,1,1),FromStatus=i%2==0?"Active":"OnLeave",ToStatus=i%2==0?"OnLeave":"Active",ActorUserId=1,AtUtc=DateTimeOffset.UtcNow});
        await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var history=await new EmployeeEmploymentService(f.Db).HistoryAsync(f.Actor,a.Id,default);
        Assert.Equal(100,history.Count);Assert.True(history[0].Id>history[^1].Id);
        Assert.DoesNotContain("ActorUserId",JsonSerializer.Serialize(history));
        Assert.DoesNotContain("AtUtc",JsonSerializer.Serialize(history));
    }}