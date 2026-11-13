using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record BulkJobSelection(int EmployeeId, byte[] Version);
public sealed record BulkJobChangeInput(int? DepartmentId, string? JobTitle, int? ManagerEmployeeId, string? EmploymentType, DateOnly EffectiveDate);
public sealed record BulkJobEmployee(int Id, string EmployeeNumber, string FullName, int DepartmentId, string? JobTitle, string? ManagerName, string? EmploymentType, bool Terminated, byte[] Version);

public sealed class EmployeeBulkJobService(PersonnelDbContext db, IOrganizationDirectory organization, TimeProvider clock)
{
    public async Task<IReadOnlyList<BulkJobEmployee>> SelectionAsync(ActorScope actor, IReadOnlyList<int> ids, CancellationToken ct)
    {
        RequireWriter(actor);
        ValidateIds(ids);
        var rows = await (from employee in db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && ids.Contains(x.Id))
                          join job in db.EmployeeJobs.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.IsCurrent)
                              on employee.Id equals job.EmployeeId into jobs
                          from job in jobs.DefaultIfEmpty()
                          join manager in db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
                              on job.ManagerEmployeeId equals (int?)manager.Id into managers
                          from manager in managers.DefaultIfEmpty()
                          orderby employee.LastName, employee.FirstName, employee.Id
                          select new BulkJobEmployee(employee.Id, employee.EmployeeNumber, employee.FirstName + " " + employee.LastName,
                              employee.DepartmentId, job == null ? null : job.JobTitle,
                              manager == null ? null : manager.FirstName + " " + manager.LastName,
                              job == null ? null : job.EmploymentType, employee.TerminationDate != null, employee.RowVersion))
            .ToListAsync(ct);
        if (rows.Count != ids.Count) throw new KeyNotFoundException("یک یا چند کارمند انتخاب‌شده در دسترس نیست.");
        return rows;
    }

    public async Task<int> ApplyAsync(ActorScope actor, IReadOnlyList<BulkJobSelection> selection, BulkJobChangeInput input, CancellationToken ct)
    {
        RequireWriter(actor);
        var ids = selection.Select(x => x.EmployeeId).ToArray();
        ValidateIds(ids);
        if (selection.Any(x => x.Version is not { Length: 8 })) throw new ArgumentException("نسخهٔ پرونده معتبر نیست؛ انتخاب‌ها را دوباره بارگذاری کنید.");
        var title = string.IsNullOrWhiteSpace(input.JobTitle) ? null : input.JobTitle.Trim();
        var type = string.IsNullOrWhiteSpace(input.EmploymentType) ? null : input.EmploymentType.Trim();
        if (input.DepartmentId is null && title is null && input.ManagerEmployeeId is null && type is null)
            throw new ArgumentException("دست‌کم یک فیلد برای تغییر انتخاب کنید.");
        if (type is not null && type is not ("FullTime" or "PartTime" or "Contract" or "Intern"))
            throw new ArgumentException("نوع استخدام معتبر نیست.");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        if (input.EffectiveDate > today) throw new ArgumentException("تاریخ مؤثر نمی‌تواند پس از امروز باشد.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Include the proposed manager in the same lock order, preventing departure during validation/save.
            var locked = new Dictionary<int, Employee>();
            foreach (var id in ids.Concat(input.ManagerEmployeeId is { } managerId ? [managerId] : Array.Empty<int>()).Distinct().Order())
            {
                var employee = await db.Employees.FromSqlInterpolated($"SELECT * FROM [personnel].[Employees] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id} AND [CompanyId]={actor.CompanyId}").SingleOrDefaultAsync(ct);
                if (employee is not null) locked.Add(id, employee);
            }
            if (ids.Any(id => !locked.ContainsKey(id))) throw new KeyNotFoundException("یک یا چند کارمند انتخاب‌شده در دسترس نیست.");
            foreach (var item in selection)
                if (!locked[item.EmployeeId].RowVersion.SequenceEqual(item.Version))
                    throw new DbUpdateConcurrencyException("یکی از پرونده‌ها تغییر کرده است؛ انتخاب‌ها را دوباره بارگذاری کنید.");
            if (input.ManagerEmployeeId is { } manager && (!locked.TryGetValue(manager, out var managerRow) || managerRow.TerminationDate is not null))
                throw new ArgumentException("مدیر انتخاب‌شده معتبر نیست.");
            if (input.DepartmentId is { } department && !await organization.DepartmentBelongsAsync(department, actor.CompanyId, ct))
                throw new ArgumentException("واحد سازمانی فعال متعلق به شرکت نیست.");
            if (title is not null && (title.Length > 200 || !await organization.ActiveJobTitleExistsAsync(actor.CompanyId, title, ct)))
                throw new ArgumentException("عنوان شغلی فعال در شرکت یافت نشد.");
            var currentJobs = await db.EmployeeJobs.Where(x => ids.Contains(x.EmployeeId) && x.IsCurrent).ToListAsync(ct);
            if (currentJobs.Any(x => x.CompanyId != actor.CompanyId) || currentJobs.GroupBy(x => x.EmployeeId).Any(g => g.Count() != 1))
                throw new ArgumentException("سابقهٔ شغلی جاری با پرونده سازگار نیست.");
            var plans = new List<(Employee Employee, EmployeeJob? Current, EmployeeJob Next)>();
            foreach (var id in ids.Order())
            {
                var employee = locked[id];
                if (employee.TerminationDate is not null) throw new ArgumentException("کارمند خارج‌شده در انتخاب‌ها وجود دارد؛ انتخاب را اصلاح کنید.");
                if (input.EffectiveDate < employee.HireDate) throw new ArgumentException("تاریخ مؤثر نمی‌تواند پیش از استخدام یکی از کارکنان باشد.");
                var current = currentJobs.SingleOrDefault(x => x.EmployeeId == id);
                var next = new EmployeeJob
                {
                    CompanyId = actor.CompanyId, EmployeeId = id, DepartmentId = input.DepartmentId ?? current?.DepartmentId ?? employee.DepartmentId,
                    JobTitle = title ?? current?.JobTitle, EmploymentType = type ?? current?.EmploymentType,
                    ManagerEmployeeId = input.ManagerEmployeeId is { } proposed && proposed != id ? proposed : current?.ManagerEmployeeId,
                    PayType = current?.PayType, PayRate = current?.PayRate, StartDate = input.EffectiveDate, IsCurrent = true
                };
                if (string.IsNullOrWhiteSpace(next.JobTitle) || next.EmploymentType is not ("FullTime" or "PartTime" or "Contract" or "Intern"))
                    throw new ArgumentException("برای پروندهٔ فاقد شغل کامل، عنوان شغلی و نوع استخدام را انتخاب کنید.");
                if (current is not null && current.DepartmentId == next.DepartmentId && current.JobTitle == next.JobTitle &&
                    current.ManagerEmployeeId == next.ManagerEmployeeId && current.EmploymentType == next.EmploymentType) continue;
                if (current is not null && input.EffectiveDate <= current.StartDate)
                    throw new ArgumentException("تاریخ مؤثر باید پس از شروع شغل جاری همهٔ کارکنان تغییرکرده باشد.");
                var currentId = current?.Id ?? 0;
                if (await db.EmployeeJobs.AnyAsync(x => x.EmployeeId == id && x.Id != currentId && (x.EndDate == null || x.EndDate >= input.EffectiveDate), ct))
                    throw new ArgumentException("تاریخ مؤثر با سابقهٔ شغلی قبلی هم‌پوشانی دارد.");
                plans.Add((employee, current, next));
            }
            // Validate the entire group first. Close rows before inserting to respect the filtered current-job index.
            foreach (var plan in plans)
            {
                plan.Employee.DepartmentId = plan.Next.DepartmentId!.Value;
                db.Entry(plan.Employee).Property(x => x.DepartmentId).IsModified = true;
                if (plan.Current is { } current) { current.IsCurrent = false; current.EndDate = input.EffectiveDate.AddDays(-1); }
            }
            if (plans.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                foreach (var plan in plans)
                {
                    db.EmployeeJobs.Add(plan.Next);
                    db.Audits.Add(new EmployeeAudit { EmployeeId = plan.Employee.Id, ActorUserId = actor.Id,
                        AtUtc = clock.GetUtcNow(), Action = "BulkJobChange", Summary = $"EffectiveDate={input.EffectiveDate:yyyy-MM-dd};Department={plan.Next.DepartmentId};Title={plan.Next.JobTitle}" });
                }
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return plans.Count;
        }
        catch
        {
            // A rendered validation error must not leave pending job/audit changes in this request's unit of work.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static void RequireWriter(ActorScope actor)
    {
        if (actor.CompanyId <= 0 || !AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
    }
    private static void ValidateIds(IReadOnlyList<int> ids)
    {
        if (ids.Count is < 1 or > 100 || ids.Any(x => x <= 0) || ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("بین ۱ تا ۱۰۰ کارمند متفاوت را انتخاب کنید.");
    }
}