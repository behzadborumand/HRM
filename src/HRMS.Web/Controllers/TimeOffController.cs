using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Security;
using HRMS.Web.Services;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Controllers
{
    [Authorize]
    public class TimeOffController : Controller
    {
        private const int PageSize = 15;

        private readonly ITimeOffRepository _timeOff;
        private readonly TimeOffService _service;
        private readonly IEmployeeRepository _employees;
        private readonly IRepository<TimeOffPolicy> _policies;

        public TimeOffController(
            ITimeOffRepository timeOff,
            TimeOffService service,
            IEmployeeRepository employees,
            IRepository<TimeOffPolicy> policies)
        {
            _timeOff = timeOff;
            _service = service;
            _employees = employees;
            _policies = policies;
        }

        [Authorize(Policy = Policies.TimeOffRead)]
        public async Task<IActionResult> Index(string? status, int page = 1, int? employeeId = null)
        {
            ViewData["Title"] = "مدیریت مرخصی";

            var canSeeAll = User.IsInRole("Admin") || User.IsInRole("Manager");
            var scopedEmployeeId = canSeeAll ? employeeId : User.EmployeeId();
            if (!canSeeAll && scopedEmployeeId is null)
            {
                return Forbid();
            }
            var year = DateTime.Now.Year;

            var model = new TimeOffIndexViewModel
            {
                EmployeeId = scopedEmployeeId ?? 0,
                Status = status,
                Page = page,
                Year = year,
                CanApprove = User.HasPermission(Permissions.TimeOffApprove)
            };

            if (scopedEmployeeId.HasValue)
            {
                var profile = await _employees.GetProfileAsync(scopedEmployeeId.Value);
                model.EmployeeName = profile?.FullName ?? User.DisplayName();
                model.Balances = await _timeOff.GetBalancesAsync(scopedEmployeeId.Value, year);
            }

            model.Requests = await _timeOff.SearchAsync(scopedEmployeeId, status, page, PageSize);
            return View(model);
        }

        [Authorize(Policy = Policies.TimeOffRequest)]
        public async Task<IActionResult> CreateRequest(int? employeeId = null)
        {
            ViewData["Title"] = "ثبت درخواست مرخصی";
            if (!User.HasPermission(Permissions.TimeOffApprove))
            {
                if (User.EmployeeId() is not int self) return Forbid();
                employeeId = self;
            }
            var model = await BuildFormAsync(employeeId ?? User.EmployeeId() ?? 0);
            return View(model);
        }

        [Authorize(Policy = Policies.TimeOffRequest)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(TimeOffRequestFormViewModel model)
        {
            ViewData["Title"] = "ثبت درخواست مرخصی";

            if (!User.HasPermission(Permissions.TimeOffApprove))
            {
                if (User.EmployeeId() is not int self) return Forbid();
                model.EmployeeId = self;
            }

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(model);
                return View(model);
            }

            var result = await _service.CreateAsync(
                model.EmployeeId, model.PolicyId, model.StartDate, model.EndDate, model.Reason, User.EmployeeId() ?? model.EmployeeId);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await PopulateLookupsAsync(model);
                return View(model);
            }

            TempData["Message"] = "درخواست مرخصی ثبت و برای مدیر ارسال شد.";
            return RedirectToAction(nameof(Index), new { employeeId = model.EmployeeId });
        }

        [Authorize(Policy = Policies.TimeOffApprove)]
        public async Task<IActionResult> Approvals()
        {
            ViewData["Title"] = "تایید درخواست‌های مرخصی";
            var employeeId = User.EmployeeId() ?? 0;

            var model = new ApprovalQueueViewModel
            {
                SeeAll = User.IsInRole("Admin")
            };

            model.Pending = model.SeeAll
                ? (await _timeOff.SearchAsync(null, "Pending", 1, 100)).Items.ToList()
                : await _timeOff.GetPendingForManagerAsync(employeeId);

            return View(model);
        }

        [Authorize(Policy = Policies.TimeOffApprove)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int requestId, string? comments)
        {
            return await DecideAsync(requestId, comments, approve: true);
        }

        [Authorize(Policy = Policies.TimeOffApprove)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int requestId, string? comments)
        {
            return await DecideAsync(requestId, comments, approve: false);
        }

        [Authorize(Policy = Policies.TimeOffRequest)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int requestId)
        {
            var result = await _service.CancelAsync(requestId, User.EmployeeId() ?? 0);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "درخواست مرخصی لغو شد."
                : string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> DecideAsync(int requestId, string? comments, bool approve)
        {
            var request = await _timeOff.GetAsync(requestId);
            var employeeId = request?.EmployeeId;
            var result = approve
                ? await _service.ApproveAsync(requestId, User.EmployeeId() ?? 0, User.IsInRole("Admin"), comments)
                : await _service.RejectAsync(requestId, User.EmployeeId() ?? 0, User.IsInRole("Admin"), comments);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? (approve ? "درخواست مرخصی تایید و از مانده کسر شد." : "درخواست مرخصی رد شد.")
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Approvals));
        }

        private async Task<TimeOffRequestFormViewModel> BuildFormAsync(int employeeId)
        {
            var model = new TimeOffRequestFormViewModel
            {
                EmployeeId = employeeId,
                CanChooseEmployee = User.HasPermission(Permissions.TimeOffApprove),
                EstimatedHours = (int)TimeOffRules.WorkingHours(DateTime.Today, DateTime.Today)
            };

            await PopulateLookupsAsync(model);
            return model;
        }

        private async Task PopulateLookupsAsync(TimeOffRequestFormViewModel model)
        {
            model.Policies = (await _policies.Query().Where(p => p.Active).ToListAsync())
                .OrderBy(p => p.PolicyName)
                .ToList();

            if (model.CanChooseEmployee)
            {
                model.Employees = await _employees.GetManagerCandidatesAsync();
            }

            if (model.EmployeeId > 0)
            {
                model.Balances = await _timeOff.GetBalancesAsync(model.EmployeeId, DateTime.Now.Year);
            }
        }
    }
}
