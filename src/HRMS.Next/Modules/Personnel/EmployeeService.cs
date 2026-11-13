using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Next.Modules.Personnel;

public sealed record EmployeeInput(int DepartmentId, string EmployeeNumber, string FirstName, string LastName, string? Email, DateOnly HireDate);
public sealed record EmployeeRow(int Id, string EmployeeNumber, string FullName, int DepartmentId, DateOnly HireDate, bool Terminated, string? Email)
{
    public bool IsOnLeave { get; init; }
    public string? JobTitle { get; init; }
    public string? ManagerName { get; init; }
    public string? PhotoSha256 { get; init; }
}
public sealed record EmployeePage(IReadOnlyList<EmployeeRow> Rows, int Total, int Page, int PageSize);

public sealed class EmployeeService(PersonnelDbContext db, IOrganizationDirectory organization, TimeProvider clock)
{
    public async Task<EmployeePage> SearchAsync(ActorScope actor, string? query, int? departmentId, int page, int pageSize, CancellationToken ct, string? status = null)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var employees = db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            employees = employees.Where(x => x.EmployeeNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term) || (x.Email != null && x.Email.Contains(term)));
        }
        if (departmentId is > 0) employees = employees.Where(x => x.DepartmentId == departmentId);
        if (status == "Active") employees = employees.Where(x => x.TerminationDate == null && !x.IsOnLeave);
        if (status == "OnLeave") employees = employees.Where(x => x.TerminationDate == null && x.IsOnLeave);
        if (status == "Terminated") employees = employees.Where(x => x.TerminationDate != null);
        var total = await employees.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling((double)total / pageSize)));
        var rows = await employees.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EmployeeRow(x.Id, x.EmployeeNumber, x.FirstName + " " + x.LastName, x.DepartmentId, x.HireDate, x.TerminationDate != null, x.Email) { IsOnLeave = x.IsOnLeave })
            .ToListAsync(ct);
        if (rows.Count > 0)
        {
            //Enrich only this bounded page. Each metadata source is independently tenant scoped.
            var ids = rows.Select(x => x.Id).ToArray();
            var facts = await (from employee in db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && ids.Contains(x.Id))
                               join job in db.EmployeeJobs.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.IsCurrent)
                                   on employee.Id equals job.EmployeeId into jobs
                               from job in jobs.DefaultIfEmpty()
                               join manager in db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
                                   on job.ManagerEmployeeId equals (int?)manager.Id into managers
                               from manager in managers.DefaultIfEmpty()
                               join photo in db.Photos.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
                                   on employee.Id equals photo.EmployeeId into photos
                               from photo in photos.DefaultIfEmpty()
                               select new { employee.Id, JobTitle = job == null ? null : job.JobTitle,
                                   ManagerName = manager == null ? null : manager.FirstName + " " + manager.LastName,
                                   PhotoSha256 = photo == null ? null : photo.Sha256 })
                .ToDictionaryAsync(x => x.Id, ct);
            rows = rows.Select(row => facts.TryGetValue(row.Id, out var fact)
                ? row with { JobTitle = fact.JobTitle, ManagerName = fact.ManagerName, PhotoSha256 = fact.PhotoSha256 }
                : row).ToList();
        }
        return new EmployeePage(rows, total, page, pageSize);
    }

    public Task<Dictionary<int, int>> DepartmentHeadcountsAsync(ActorScope actor, CancellationToken ct)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        return db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.TerminationDate == null)
            .GroupBy(x => x.DepartmentId).Select(x => new { DepartmentId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, ct);
    }
    public async Task<Employee?> GetAsync(ActorScope actor, int id, CancellationToken ct)
    {
        await PersonnelReadScope.RequireAsync(db, actor, id, ct);
        return await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == actor.CompanyId, ct);
    }

    public async Task<int> CreateAsync(ActorScope actor, EmployeeInput input, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        await ValidateAsync(actor.CompanyId, input, ct);
        var number = input.EmployeeNumber.Trim();
        if (await db.Employees.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeNumber == number, ct))
            throw new ArgumentException("شماره پرسنلی تکراری است.");
        var employee = new Employee
        {
            CompanyId = actor.CompanyId, DepartmentId = input.DepartmentId, EmployeeNumber = number,
            FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), Email = input.Email?.Trim(), HireDate = input.HireDate
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        db.Audits.Add(new EmployeeAudit
        {
            EmployeeId = employee.Id, ActorUserId = actor.Id, AtUtc = clock.GetUtcNow(), Action = "Create",
            Summary = $"EmployeeNumber={employee.EmployeeNumber}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return employee.Id;
    }

    public async Task UpdateAsync(ActorScope actor, int id, EmployeeInput input, byte[] expectedVersion, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var employee = await db.Employees.FromSqlInterpolated($"SELECT * FROM [personnel].[Employees] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id} AND [CompanyId]={actor.CompanyId}").SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("کارمند یافت نشد.");
        await ValidateAsync(actor.CompanyId, input, ct, input.DepartmentId == employee.DepartmentId);
        if (!employee.RowVersion.SequenceEqual(expectedVersion)) throw new DbUpdateConcurrencyException("پرونده توسط کاربر دیگری تغییر کرده است.");
        if (input.DepartmentId != employee.DepartmentId)
            throw new ArgumentException("تغییر واحد سازمانی باید در بخش ثبت تغییر شغل انجام شود.");
        if (await db.EmployeeJobs.AnyAsync(x => x.EmployeeId == id && x.StartDate < input.HireDate, ct))
            throw new ArgumentException("تاریخ استخدام نمی‌تواند پس از شروع یکی از سوابق شغلی باشد.");
        if (await db.EmploymentStatusChanges.AnyAsync(x => x.EmployeeId == id && x.CompanyId == actor.CompanyId && x.EffectiveDate < input.HireDate, ct))
            throw new ArgumentException("تاریخ استخدام نمی‌تواند پس از یکی از تغییرات وضعیت اشتغال باشد.");
        if (await db.PersonalInfos.AnyAsync(p => p.EmployeeId == id && p.CompanyId == actor.CompanyId && p.DateOfBirth > input.HireDate, ct))
            throw new ArgumentException("تاریخ استخدام نمی‌تواند پیش از تاریخ تولد باشد.");
        var number = input.EmployeeNumber.Trim();
        if (await db.Employees.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Id != id && x.EmployeeNumber == number, ct))
            throw new ArgumentException("شماره پرسنلی تکراری است.");
        var before = $"Department={employee.DepartmentId};Number={employee.EmployeeNumber};Name={employee.FirstName} {employee.LastName};HireDate={employee.HireDate:yyyy-MM-dd}";
        employee.DepartmentId = input.DepartmentId;
        employee.EmployeeNumber = number;
        employee.FirstName = input.FirstName.Trim();
        employee.LastName = input.LastName.Trim();
        employee.Email = input.Email?.Trim();
        employee.HireDate = input.HireDate;
        db.Audits.Add(new EmployeeAudit
        {
            EmployeeId = id, ActorUserId = actor.Id, AtUtc = clock.GetUtcNow(), Action = "Update",
            Summary = before + $" -> Department={employee.DepartmentId};Number={employee.EmployeeNumber};Name={employee.FirstName} {employee.LastName};HireDate={employee.HireDate:yyyy-MM-dd}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ValidateAsync(int companyId, EmployeeInput input, CancellationToken ct, bool allowExistingInactiveDepartment = false)
    {
        if (string.IsNullOrWhiteSpace(input.EmployeeNumber) || string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName))
            throw new ArgumentException("شماره پرسنلی، نام و نام خانوادگی الزامی است.");
        if (input.EmployeeNumber.Trim().Length > 50 || input.FirstName.Trim().Length > 100 || input.LastName.Trim().Length > 100)
            throw new ArgumentException("طول شماره پرسنلی یا نام معتبر نیست.");
        if (input.Email is { Length: > 0 } email && (email.Length > 256 || email.IndexOfAny([';', ',', ' ', '\t', '\r', '\n']) >= 0 || !new EmailAddressAttribute().IsValid(email)))
            throw new ArgumentException("ایمیل معتبر نیست.");
        var departmentExists = allowExistingInactiveDepartment
            ? await organization.DepartmentExistsAsync(input.DepartmentId, companyId, ct)
            : await organization.DepartmentBelongsAsync(input.DepartmentId, companyId, ct);
        if (!departmentExists)
            throw new ArgumentException("واحد سازمانی معتبر نیست.");
    }
}
