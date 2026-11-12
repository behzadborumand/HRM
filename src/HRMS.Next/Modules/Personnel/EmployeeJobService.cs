using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record JobHistoryRow(int Id, DateOnly StartDate, DateOnly? EndDate, bool IsCurrent,
    int? DepartmentId, string? JobTitle, int? ManagerEmployeeId, string? ManagerName, string? EmploymentType);

public sealed record JobChangeInput(int DepartmentId, string JobTitle, int? ManagerEmployeeId, string EmploymentType, DateOnly EffectiveDate);

public sealed class EmployeeJobService(PersonnelDbContext db, IOrganizationDirectory organization, TimeProvider clock)
{
    public async Task ChangeAsync(ActorScope actor, int employeeId, JobChangeInput input, byte[] expectedVersion, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new KeyNotFoundException("کارمند یافت نشد.");
        if (!employee.RowVersion.SequenceEqual(expectedVersion))
            throw new DbUpdateConcurrencyException("پرونده توسط کاربر دیگری تغییر کرده است.");
        if (employee.TerminationDate is not null) throw new ArgumentException("برای کارمند خارج‌شده نمی‌توان شغل جاری ثبت کرد.");
        if (string.IsNullOrWhiteSpace(input.JobTitle) || input.JobTitle.Trim().Length > 200)
            throw new ArgumentException("عنوان شغلی الزامی است و باید حداکثر ۲۰۰ نویسه باشد.");
        if (!await organization.ActiveJobTitleExistsAsync(actor.CompanyId, input.JobTitle.Trim(), ct))
            throw new ArgumentException("عنوان شغلی فعال در شرکت یافت نشد.");
        if (input.EmploymentType is not ("FullTime" or "PartTime" or "Contract" or "Intern"))
            throw new ArgumentException("نوع استخدام معتبر نیست.");
        var iranToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        if (input.EffectiveDate < employee.HireDate || input.EffectiveDate > iranToday)
            throw new ArgumentException("تاریخ مؤثر باید بین تاریخ استخدام و امروز باشد.");
        if (!await organization.DepartmentBelongsAsync(input.DepartmentId, actor.CompanyId, ct))
            throw new ArgumentException("واحد سازمانی متعلق به شرکت نیست.");
        if (input.ManagerEmployeeId is { } managerId &&
            (managerId == employeeId || !await db.Employees.AsNoTracking().AnyAsync(x =>
                x.Id == managerId && x.CompanyId == actor.CompanyId && x.TerminationDate == null, ct)))
            throw new ArgumentException("مدیر انتخاب‌شده معتبر نیست.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.EmployeeJobs.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsCurrent, ct);
        if (current is not null && current.CompanyId != actor.CompanyId)
            throw new InvalidOperationException("شرکت رکورد شغلی جاری با پرونده سازگار نیست.");
        if (current is not null && input.EffectiveDate <= current.StartDate)
            throw new ArgumentException("تاریخ مؤثر باید پس از شروع شغل جاری باشد.");
        var currentId = current?.Id ?? 0;
        if (await db.EmployeeJobs.AnyAsync(x => x.EmployeeId == employeeId && x.Id != currentId &&
            (x.EndDate == null || x.EndDate >= input.EffectiveDate), ct))
            throw new ArgumentException("تاریخ مؤثر با سابقه شغلی قبلی هم‌پوشانی دارد.");
        employee.DepartmentId = input.DepartmentId;
        db.Entry(employee).Property(x => x.DepartmentId).IsModified = true;
        await db.SaveChangesAsync(ct);
        if (current is not null)
        {
            current.IsCurrent = false;
            current.EndDate = input.EffectiveDate.AddDays(-1);
            await db.SaveChangesAsync(ct);
        }
        var next = new EmployeeJob
        {
            CompanyId = actor.CompanyId, EmployeeId = employeeId, DepartmentId = input.DepartmentId,
            ManagerEmployeeId = input.ManagerEmployeeId, JobTitle = input.JobTitle.Trim(),
            EmploymentType = input.EmploymentType, PayType = current?.PayType, PayRate = current?.PayRate,
            StartDate = input.EffectiveDate, IsCurrent = true
        };
        db.EmployeeJobs.Add(next);
        db.Audits.Add(new EmployeeAudit
        {
            EmployeeId = employeeId, ActorUserId = actor.Id, AtUtc = clock.GetUtcNow(),
            Action = "JobChange", Summary = $"EffectiveDate={input.EffectiveDate:yyyy-MM-dd};Department={input.DepartmentId};Title={next.JobTitle}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Employee>> ManagerOptionsAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        return await db.Employees.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.Id != employeeId && x.TerminationDate == null)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<JobHistoryRow>> HistoryAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!AccessRules.CanReadEmployee(actor, actor.CompanyId, employeeId)) throw new UnauthorizedAccessException();
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId && x.CompanyId == actor.CompanyId, ct))
            return [];
        return await db.EmployeeJobs.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Select(x => new JobHistoryRow(x.Id, x.StartDate, x.EndDate, x.IsCurrent, x.DepartmentId,
                x.JobTitle, x.ManagerEmployeeId,
                db.Employees.Where(m => m.Id == x.ManagerEmployeeId && m.CompanyId == actor.CompanyId)
                    .Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(), x.EmploymentType))
            .ToListAsync(ct);
    }
}
