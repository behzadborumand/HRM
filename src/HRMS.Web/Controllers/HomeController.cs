using System.Diagnostics;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Security;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.Web.Models;

namespace HRMS.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IReportingRepository _reporting;
    private readonly ITimeOffRepository _timeOff;
    private readonly IEmployeeRepository _employees;
    private readonly IDashboardRepository _dashboard;

    public HomeController(IReportingRepository reporting, ITimeOffRepository timeOff, IEmployeeRepository employees, IDashboardRepository dashboard)
    {
        _reporting = reporting;
        _timeOff = timeOff;
        _employees = employees;
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "داشبورد";
        var employeeId = User.EmployeeId();
        var today = DateTime.Today;
        var canReadTimeOff = User.HasPermission(Permissions.TimeOffRead);
        var canBrowsePeople = User.HasPermission(Permissions.EmployeesRead);
        var canViewOrganization = User.HasPermission(Permissions.ReportsRead);

        var model = new DashboardViewModel
        {
            Stats = canViewOrganization ? await _reporting.GetDashboardAsync() : new(),
            CanViewOrganization = canViewOrganization,
            CanBrowsePeople = canBrowsePeople,
            EmployeeName = User.DisplayName(),
            CurrentEmployee = employeeId.HasValue ? await _employees.GetProfileAsync(employeeId.Value) : null,
            BalanceYear = today.Year
        };

        model.OrganizationLinks = (await _dashboard.GetActiveLinksAsync())
            .Where(l => l.Active && OrganizationLinkRules.IsAllowedUrl(l.Url)).ToList();
        if (canBrowsePeople)
        {
            var people = await _dashboard.GetCurrentPeopleAsync(today);
            model.Celebrations = DashboardHighlightsRules.Celebrations(people, today);
            model.NewHires = DashboardHighlightsRules.NewHires(people, today);
        }

        if (employeeId.HasValue && canReadTimeOff)
        {
            model.MyBalances = await _timeOff.GetBalancesAsync(employeeId.Value, model.BalanceYear);
            model.RecentRequests = await _timeOff.GetRecentForEmployeeAsync(employeeId.Value);
        }

        if (User.HasPermission(Permissions.TimeOffApprove) && (User.IsInRole("Admin") || employeeId.HasValue))
        {
            model.AwaitingMyApproval = User.IsInRole("Admin")
                ? (await _timeOff.SearchAsync(null, "Pending", 1, 6)).Items.ToList()
                : await _timeOff.GetPendingForManagerAsync(employeeId!.Value);
        }

        if (canBrowsePeople || (canReadTimeOff && employeeId.HasValue))
        {
            var captions = new[] { "امروز", "فردا", "پس‌فردا" };
            for (var offset = 0; offset < captions.Length; offset++)
            {
                var date = today.AddDays(offset);
                var rows = await _timeOff.GetOutOnAsync(date, canBrowsePeople ? null : employeeId);
                model.AbsenceDays.Add(new DashboardAbsenceDay(date, captions[offset],
                    rows.GroupBy(r => r.EmployeeId).Select(g => g.First()).ToList()));
            }
        }

        var ownId = model.CurrentEmployee?.EmployeeId;
        if (ownId.HasValue && User.HasPermission(Permissions.TimeOffRequest))
            model.Shortcuts.Add(new("درخواست مرخصی", "ثبت درخواست جدید", "bi-calendar-plus", "TimeOff", "CreateRequest"));
        if (canBrowsePeople)
            model.Shortcuts.Add(new("فهرست همکاران", "جستجو و مشاهدهٔ پرونده‌ها", "bi-people", "Employees", "Index"));
        if (ownId.HasValue)
            model.Shortcuts.Add(new("اطلاعات من", "پروندهٔ شخصی و شغلی", "bi-person-circle", "Home", "Profile"));
        if (ownId.HasValue || canBrowsePeople)
            model.Shortcuts.Add(new("آموزش‌ها", "دوره‌ها و سررسیدها", "bi-mortarboard", "Training", "Index", ownId));
        if (User.HasPermission(Permissions.TimeOffApprove) && (User.IsInRole("Admin") || employeeId.HasValue))
            model.Shortcuts.Add(new("تأیید درخواست‌ها", "درخواست‌های منتظر بررسی", "bi-check2-circle", "TimeOff", "Approvals"));
        else if (ownId.HasValue && canReadTimeOff)
            model.Shortcuts.Add(new("پیگیری درخواست‌ها", "وضعیت مرخصی‌های من", "bi-calendar-check", "TimeOff", "Index", ownId));
        if (ownId.HasValue)
            model.Shortcuts.Add(new("مدارک من", "اسناد و تاریخ اعتبار", "bi-folder2-open", "Documents", "Index", ownId));

        return View(model);
    }

    public IActionResult Profile()
    {
        var employeeId = User.EmployeeId();
        return employeeId is null ? RedirectToAction(nameof(Index)) : RedirectToAction("Details", "Employees", new { id = employeeId });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
