using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace HRMS.Next.Modules.Personnel;

public sealed class EmployeePersonalInfo
{
    public int EmployeeId { get; set; }
    public int CompanyId { get; set; }
    public string? PreferredName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed record PersonalContactInput(string? PreferredName, string? HomePhone, string? MobilePhone,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? PostalCode, string? Country,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation);
public sealed record PersonalIdentityInput(string? Phone, DateOnly? DateOfBirth, string? Gender, string? MaritalStatus);
public sealed class EmployeePersonalService(PersonnelDbContext db, TimeProvider clock)
{
    public static bool CanRead(ActorScope actor, int employeeId) => actor is { IsActive:true, CompanyId:>0 } &&
        employeeId>0 && (actor.IsAdmin || actor.EmployeeId==employeeId);

    public async Task<EmployeePersonalInfo?> ReadAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!CanRead(actor,employeeId)) throw new UnauthorizedAccessException();
        return await db.PersonalInfos.AsNoTracking().SingleOrDefaultAsync(x=>x.EmployeeId==employeeId&&x.CompanyId==actor.CompanyId,ct);
    }

    public async Task SaveContactAsync(ActorScope actor,int employeeId,PersonalContactInput input,byte[]? version,CancellationToken ct)
    {
        if(!CanRead(actor,employeeId))throw new UnauthorizedAccessException();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        await RequireWriteAsync(actor,employeeId,false,ct);
        // Normalize before attaching a record so failed validation cannot leave partial tracked updates.
        var value=new PersonalContactInput(Text(input.PreferredName,100),Phone(input.HomePhone),Phone(input.MobilePhone),
            Text(input.AddressLine1,200,true),Text(input.AddressLine2,200,true),Text(input.City,100),Text(input.State,100),
            Digits(Text(input.PostalCode,20)),Text(input.Country,100),Text(input.EmergencyContactName,200),
            Phone(input.EmergencyContactPhone),Text(input.EmergencyContactRelation,100));
        var info=await ForUpdateAsync(actor,employeeId,version,ct);
        info.PreferredName=value.PreferredName;info.HomePhone=value.HomePhone;info.MobilePhone=value.MobilePhone;
        info.AddressLine1=value.AddressLine1;info.AddressLine2=value.AddressLine2;info.City=value.City;info.State=value.State;
        info.PostalCode=value.PostalCode;info.Country=value.Country;info.EmergencyContactName=value.EmergencyContactName;
        info.EmergencyContactPhone=value.EmergencyContactPhone;info.EmergencyContactRelation=value.EmergencyContactRelation;
        await SaveAsync(actor,info,"PersonalContact",ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SaveIdentityAsync(ActorScope actor,int employeeId,PersonalIdentityInput input,byte[]? version,CancellationToken ct)
    {
        if(!CanRead(actor,employeeId)||!actor.IsAdmin)throw new UnauthorizedAccessException();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var employee=await RequireWriteAsync(actor,employeeId,true,ct);
        var phone=Phone(input.Phone);var gender=Text(input.Gender,20);var marital=Text(input.MaritalStatus,20);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        if(input.DateOfBirth is {} birth && (birth<DateOnly.FromDateTime(new System.Globalization.PersianCalendar().MinSupportedDateTime) || birth>today || birth>employee.HireDate))
            throw new ArgumentException("تاریخ تولد باید پیش از استخدام و حداکثر امروز باشد.");
        var info=await ForUpdateAsync(actor,employeeId,version,ct);
        info.Phone=phone;info.DateOfBirth=input.DateOfBirth;info.Gender=gender;info.MaritalStatus=marital;
        await SaveAsync(actor,info,"PersonalIdentity",ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Employee> RequireWriteAsync(ActorScope actor,int employeeId,bool identity,CancellationToken ct)
    {
        if(!CanRead(actor,employeeId) || (identity&&!actor.IsAdmin)) throw new UnauthorizedAccessException();
        var employee=await db.Employees.FromSqlInterpolated($"SELECT * FROM [personnel].[Employees] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={employeeId} AND [CompanyId]={actor.CompanyId}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException();
        if(!actor.IsAdmin&&employee.TerminationDate is not null) throw new UnauthorizedAccessException();
        return employee;
    }

    private async Task<EmployeePersonalInfo> ForUpdateAsync(ActorScope actor,int employeeId,byte[]? version,CancellationToken ct)
    {
        foreach (var tracked in db.ChangeTracker.Entries<EmployeePersonalInfo>().Where(e=>e.Entity.EmployeeId==employeeId).ToList()) tracked.State=EntityState.Detached;
        var info=await db.PersonalInfos.SingleOrDefaultAsync(x=>x.EmployeeId==employeeId&&x.CompanyId==actor.CompanyId,ct);
        if((info is null&&version is not null)||(info is not null&&(version is null||!info.RowVersion.SequenceEqual(version))))
            throw new DbUpdateConcurrencyException("اطلاعات شخصی تغییر کرده است؛ صفحه را بازخوانی کنید.");
        if(info is not null)return info;
        info=new(){EmployeeId=employeeId,CompanyId=actor.CompanyId};db.PersonalInfos.Add(info);return info;
    }

    private async Task SaveAsync(ActorScope actor,EmployeePersonalInfo info,string action,CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();var entry=db.Entry(info);
        var changed=entry.Properties.Where(p=>p.Metadata.Name is not (nameof(EmployeePersonalInfo.EmployeeId) or nameof(EmployeePersonalInfo.CompanyId) or nameof(EmployeePersonalInfo.RowVersion)) &&
            (p.IsModified||(entry.State==EntityState.Added&&p.CurrentValue is not null))).Select(p=>p.Metadata.Name).ToList();
        if(entry.State==EntityState.Unchanged)return;
        var audit=new EmployeeAudit{EmployeeId=info.EmployeeId,ActorUserId=actor.Id,AtUtc=clock.GetUtcNow(),Action=action,Summary="Fields="+string.Join(",",changed)};
        db.Audits.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch(Exception error)
        {
            db.Entry(audit).State=EntityState.Detached;
            db.Entry(info).State=EntityState.Detached;
            if(error is DbUpdateException {InnerException:SqlException {Number:2601 or 2627}})
                throw new DbUpdateConcurrencyException("اطلاعات شخصی هم‌زمان ثبت شده است؛ صفحه را بازخوانی کنید.",error);
            throw;
        }
    }

    private static string? Text(string? value,int limit,bool multiline=false)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        var text=value.Trim();
        if(text.Length>limit||text.Any(c=>char.IsControl(c)&&!(multiline&&c is '\r' or '\n')))
            throw new ArgumentException("طول یا نویسه‌های اطلاعات شخصی معتبر نیست.");
        return text;
    }
    private static string? Digits(string? value)=>value is null?null:string.Concat(value.Select(c=>c is >= '۰' and <= '۹'?(char)('0'+c-'۰'):c is >= '٠' and <= '٩'?(char)('0'+c-'٠'):c));
    private static string? Phone(string? value)
    {
        var phone=Digits(Text(value,50));
        if(phone is not null && (!phone.Any(char.IsAsciiDigit)||phone.Any(c=>!char.IsAsciiDigit(c)&&!" +()-#.xX".Contains(c))))
            throw new ArgumentException("شمارهٔ تماس معتبر نیست.");
        return phone;
    }
}
