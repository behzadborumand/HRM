using System.ComponentModel.DataAnnotations;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class BulkEditModel(AccessService access, EmployeeBulkJobService bulk, EmployeeJobService jobs,
    IOrganizationDirectory organization, JobTitleService titles) : PageModel
{
    [BindProperty] public BulkJobForm Form { get; set; } = new();
    [TempData] public string? Message { get; set; }
    public IReadOnlyList<BulkJobEmployee> Employees { get; private set; } = [];
    public IReadOnlyList<DepartmentOption> Departments { get; private set; } = [];
    public IReadOnlyList<DepartmentOption> AllDepartments { get; private set; } = [];
    public IReadOnlyList<JobTitleOption> JobTitles { get; private set; } = [];
    public IReadOnlyList<Employee> Managers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int[] ids, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || actor.CompanyId <= 0 || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        if (!ModelState.IsValid) return BadRequest();
        try { Employees = await bulk.SelectionAsync(actor, ids, ct); }
        catch (ArgumentException) { return BadRequest(); }
        catch (KeyNotFoundException) { return NotFound(); }
        Form.Selected = Employees.Select(x => new BulkJobSelectedForm { EmployeeId = x.Id, Version = Convert.ToBase64String(x.Version) }).ToList();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        Form.EffectiveDateJalali = JalaliDate.Format(today);
        await LoadOptionsAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || actor.CompanyId <= 0 || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        try { Employees = await bulk.SelectionAsync(actor, Form.Selected.Select(x => x.EmployeeId).ToArray(), ct); }
        catch (ArgumentException) { return BadRequest(); }
        catch (KeyNotFoundException) { return NotFound(); }
        await LoadOptionsAsync(actor, ct);
        if (!ModelState.IsValid) return Page();
        try
        {
            if (!JalaliDate.TryParse(Form.EffectiveDateJalali, out var date)) throw new ArgumentException("تاریخ مؤثر شمسی معتبر نیست.");
            var selection = Form.Selected.Select(x => new BulkJobSelection(x.EmployeeId, Convert.FromBase64String(x.Version))).ToArray();
            var changed = await bulk.ApplyAsync(actor, selection, new(Form.DepartmentId, Form.JobTitle, Form.ManagerEmployeeId, Form.EmploymentType, date) { EmploymentStatus = Form.EmploymentStatus }, ct);
            Message = changed == 0 ? "هیچ تغییری لازم نبود؛ سابقهٔ تکراری ثبت نشد." : $"تغییرات برای {changed} کارمند ثبت شد.";
            return RedirectToPage(new { ids = Form.Selected.Select(x => x.EmployeeId).ToArray() });
        }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ پرونده نامعتبر است؛ انتخاب‌ها را دوباره بارگذاری کنید."); }
        catch (DbUpdateConcurrencyException error) { ModelState.AddModelError("", error.Message); }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
        return Page();
    }

    private async Task LoadOptionsAsync(ActorScope actor, CancellationToken ct)
    {
        Departments = await organization.ActiveDepartmentsAsync(actor.CompanyId, ct);
        AllDepartments = await organization.DepartmentsAsync(actor.CompanyId, ct);
        JobTitles = await titles.ActiveOptionsAsync(actor, ct);
        Managers = await jobs.ManagerOptionsAsync(actor, 0, ct);
    }
}

public sealed class BulkJobForm
{
    public List<BulkJobSelectedForm> Selected { get; set; } = [];
    [Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
    [StringLength(200)] public string? JobTitle { get; set; }
    [Range(1, int.MaxValue)] public int? ManagerEmployeeId { get; set; }
    [StringLength(20)] public string? EmploymentStatus { get; set; }
    [StringLength(50)] public string? EmploymentType { get; set; }
    [Required(ErrorMessage = "تاریخ مؤثر الزامی است.")] public string EffectiveDateJalali { get; set; } = "";
}
public sealed class BulkJobSelectedForm
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    [Required] public string Version { get; set; } = "";
}