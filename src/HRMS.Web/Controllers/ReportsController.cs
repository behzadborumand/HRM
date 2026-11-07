using System.Text;
using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Web.Helpers;
using HRMS.Web.Security;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Web.Controllers
{
    [Authorize(Policy = Policies.ReportsRead)]
    public class ReportsController : Controller
    {
        private const int ReportRowLimit = 500;

        private readonly IEmployeeRepository _employees;
        private readonly ITimeOffRepository _timeOff;
        private readonly IReportingRepository _reporting;
        private readonly IRepository<Department> _departments;

        public ReportsController(
            IEmployeeRepository employees,
            ITimeOffRepository timeOff,
            IReportingRepository reporting,
            IRepository<Department> departments)
        {
            _employees = employees;
            _timeOff = timeOff;
            _reporting = reporting;
            _departments = departments;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "گزارش‌ها";
            return View(await _reporting.GetDashboardAsync());
        }

        public async Task<IActionResult> EmployeesList(string? keyword, int? departmentId, string? status)
        {
            ViewData["Title"] = "گزارش لیست کارکنان";

            var result = await _employees.SearchAsync(keyword, departmentId, status, 1, ReportRowLimit);

            return View(new EmployeesReportViewModel
            {
                Rows = result.Items.ToList(),
                Keyword = keyword,
                DepartmentId = departmentId,
                Status = status,
                Departments = await _departments.ListAsync()
            });
        }

        public async Task<IActionResult> ExportEmployees(string? keyword, int? departmentId, string? status)
        {
            var result = await _employees.SearchAsync(keyword, departmentId, status, 1, ReportRowLimit);

            var rows = result.Items.Select(e => new[]
            {
                e.EmployeeNumber ?? string.Empty,
                e.FullName,
                e.Email ?? string.Empty,
                e.Phone ?? string.Empty,
                e.DepartmentName ?? string.Empty,
                e.JobTitleName ?? string.Empty,
                e.ManagerName ?? string.Empty,
                PersianDate.ToJalali(e.HireDate),
                e.EmploymentStatus,
                e.PayRate?.ToString("0") ?? string.Empty
            });

            return Csv("karmanan", new[]
            {
                "کد پرسنلی", "نام و نام خانوادگی", "ایمیل", "تلفن", "واحد سازمانی", "عنوان شغلی", "مدیر مستقیم", "تاریخ استخدام", "وضعیت", "حقوق"
            }, rows);
        }

        public async Task<IActionResult> TimeOffSummary(int? year, int? departmentId)
        {
            ViewData["Title"] = "گزارش خلاصه مرخصی";

            return View(new TimeOffReportViewModel
            {
                Year = year ?? DateTime.Now.Year,
                DepartmentId = departmentId,
                Departments = await _departments.ListAsync(),
                Rows = await _timeOff.GetSummaryAsync(year ?? DateTime.Now.Year, departmentId)
            });
        }

        public async Task<IActionResult> ExportTimeOff(int? year, int? departmentId)
        {
            var rows = await _timeOff.GetSummaryAsync(year ?? DateTime.Now.Year, departmentId);

            return Csv("morkhasi", new[]
            {
                "کارمند", "واحد سازمانی", "نوع مرخصی", "ساعت تعلق‌گرفته", "ساعت استفاده‌شده", "مانده", "تعداد درخواست"
            }, rows.Select(r => new[]
            {
                r.EmployeeName,
                r.DepartmentName ?? string.Empty,
                r.PolicyName,
                r.AccruedHours.ToString("0.##"),
                r.UsedHours.ToString("0.##"),
                r.AvailableHours.ToString("0.##"),
                r.RequestCount.ToString()
            }));
        }

        private FileContentResult Csv(string fileName, string[] header, IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", header.Select(Escape)));

            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(",", row.Select(Escape)));
            }

            // BOM keeps Excel from mangling Persian text in exported CSV files.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"{fileName}-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string Escape(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
