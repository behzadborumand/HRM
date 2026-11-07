using System.Security.Claims;
using HRMS.Core.Dtos;
using HRMS.Core.Paging;
using HRMS.Core.Repositories;
using HRMS.Web.Controllers;
using HRMS.Web.Security;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HRMS.Tests;

public class DashboardAccessTests
{
    [Fact]
    public async Task Employee_home_never_queries_other_employees_requests_or_global_reports()
    {
        var reports = new ReportsFake();
        var timeOff = new TimeOffFake();
        var controller = new HomeController(reports, timeOff, new EmployeesFake(), new DashboardFake())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "کاربر آزمایشی"), new Claim(ClaimTypes.Role, "Employee"),
                    new Claim(HrmsClaims.EmployeeId, "7"), new Claim(HrmsClaims.Permission, Permissions.TimeOffRead)
                }, "test"))
            } }
        };
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);

        Assert.Equal(new int?[] { 7 }, timeOff.RequestScopes);
        Assert.DoesNotContain(timeOff.RequestScopes, id => id is null || id != 7);
        Assert.Equal(0, reports.Calls);
        Assert.Equal(3, model.AbsenceDays.Count);
        Assert.All(timeOff.AbsenceScopes, id => Assert.Equal(7, id));
        Assert.All(model.AbsenceDays, day => Assert.Single(day.People));
        Assert.DoesNotContain(model.Shortcuts, s => s.Controller == "Employees" || s.Action == "Approvals");
    }

    [Fact]
    public async Task Missing_employee_claim_never_becomes_an_unscoped_personal_query()
    {
        var timeOff = new TimeOffFake();
        var controller = Create(timeOff, "Employee", null, Permissions.TimeOffRead, Permissions.TimeOffRequest);
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);
        Assert.Empty(timeOff.RequestScopes);
        Assert.Empty(timeOff.AbsenceScopes);
        Assert.Empty(model.Shortcuts);
        Assert.Null(model.CurrentEmployee);
    }

    [Fact]
    public async Task Managers_load_only_their_approval_queue()
    {
        var timeOff = new TimeOffFake();
        var controller = Create(timeOff, "Manager", 7, Permissions.TimeOffApprove, Permissions.EmployeesRead);
        await controller.Index();
        Assert.Equal(new[] { 7 }, timeOff.ManagerScopes);
        Assert.Empty(timeOff.RequestScopes);
        Assert.All(timeOff.AbsenceScopes, id => Assert.Null(id));
    }

    [Fact]
    public async Task Admin_without_a_personal_record_can_still_open_the_authorized_approval_queue()
    {
        var timeOff = new TimeOffFake();
        var controller = Create(timeOff, "Admin", null);
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);
        Assert.Equal("Pending", Assert.Single(timeOff.SearchStatuses));
        Assert.Null(Assert.Single(timeOff.RequestScopes));
        Assert.Empty(model.RecentRequests);
        Assert.True(model.CanViewOrganization);
        Assert.Contains(model.Shortcuts, s => s.Action == "Approvals");
    }

    [Fact]
    public async Task A_user_without_time_off_permissions_gets_no_time_off_queries_or_shortcuts()
    {
        var timeOff = new TimeOffFake();
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await Create(timeOff, "Employee", 7).Index()).Model);
        Assert.Empty(timeOff.RequestScopes);
        Assert.Empty(timeOff.AbsenceScopes);
        Assert.Empty(timeOff.BalanceScopes);
        Assert.DoesNotContain(model.Shortcuts, s => s.Controller == "TimeOff");
    }

    private static HomeController Create(TimeOffFake timeOff, string role, int? employeeId, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "آزمایشی"), new(ClaimTypes.Role, role) };
        if (employeeId.HasValue) claims.Add(new(HrmsClaims.EmployeeId, employeeId.Value.ToString()));
        claims.AddRange(permissions.Select(p => new Claim(HrmsClaims.Permission, p)));
        return new HomeController(new ReportsFake(), timeOff, new EmployeesFake(), new DashboardFake())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } }
        };
    }

    [Fact]
    public async Task Direct_time_off_navigation_without_employee_claim_is_denied_before_querying()
    {
        var timeOff = new TimeOffFake();
        var controller = new TimeOffController(timeOff, null!, new EmployeesFake(), null!)
        {
            ControllerContext = Create(timeOff, "Employee", null, Permissions.TimeOffRead).ControllerContext
        };
        Assert.IsType<ForbidResult>(await controller.Index(null));
        Assert.Empty(timeOff.RequestScopes);
    }

    [Fact]
    public async Task Direct_training_navigation_without_employee_claim_is_denied_before_querying()
    {
        var controller = new TrainingController(null!, null!, null!)
        {
            ControllerContext = Create(new TimeOffFake(), "Employee", null).ControllerContext
        };
        Assert.IsType<ForbidResult>(await controller.Index(null, null, null, null));
    }

    [Fact]
    public async Task Leave_form_without_employee_claim_cannot_target_another_employee()
    {
        var timeOff = new TimeOffFake();
        var controller = new TimeOffController(timeOff, null!, new EmployeesFake(), null!)
        {
            ControllerContext = Create(timeOff, "Employee", null, Permissions.TimeOffRequest).ControllerContext
        };
        Assert.IsType<ForbidResult>(await controller.CreateRequest((int?)99));
    }

    [Fact]
    public async Task Leave_submission_without_employee_claim_cannot_target_another_employee()
    {
        var timeOff = new TimeOffFake();
        var controller = new TimeOffController(timeOff, null!, new EmployeesFake(), null!)
        {
            ControllerContext = Create(timeOff, "Employee", null, Permissions.TimeOffRequest).ControllerContext
        };
        Assert.IsType<ForbidResult>(await controller.CreateRequest(new TimeOffRequestFormViewModel { EmployeeId = 99 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Organization_highlights_are_fetched_only_with_employee_read_permission(bool allowed)
    {
        var source = new DashboardFake();
        var controller = new HomeController(new ReportsFake(), new TimeOffFake(), new EmployeesFake(), source)
        {
            ControllerContext = Create(new TimeOffFake(), "Employee", 7, allowed ? Permissions.EmployeesRead : Permissions.TimeOffRead).ControllerContext
        };
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);
        Assert.Equal(allowed ? 1 : 0, source.PeopleCalls);
        Assert.Equal(allowed, model.CanBrowsePeople);
        Assert.Equal(allowed ? 1 : 0, model.Celebrations.Count);
        Assert.Equal(allowed ? 1 : 0, model.NewHires.Count);
        Assert.Equal("وبگاه", Assert.Single(model.OrganizationLinks).Title);
    }

    private sealed class DashboardFake : IDashboardRepository
    {
        public int PeopleCalls { get; private set; }
        public Task<List<DashboardPerson>> GetCurrentPeopleAsync(DateTime today)
        {
            PeopleCalls++;
            return Task.FromResult(new List<DashboardPerson> {new(9, "همکار", today.AddYears(-25), today, "کارشناس", false)});
        }
        public Task<List<HRMS.Core.Entities.OrganizationLink>> GetActiveLinksAsync() => Task.FromResult(new List<HRMS.Core.Entities.OrganizationLink>
        {
            new() { Title = "وبگاه", Url = "https://example.com", Active = true },
            new() { Title = "غیرفعال", Url = "https://example.com", Active = false },
            new() { Title = "نامعتبر", Url = "javascript:alert(1)", Active = true }
        });
    }

    private sealed class ReportsFake : IReportingRepository
    {
        public int Calls { get; private set; }
        public Task<DashboardStats> GetDashboardAsync() { Calls++; return Task.FromResult(new DashboardStats()); }
    }

    private sealed class TimeOffFake : ITimeOffRepository
    {
        public List<int?> RequestScopes { get; } = new();
        public List<string?> SearchStatuses { get; } = new();
        public List<int?> AbsenceScopes { get; } = new();
        public List<int> BalanceScopes { get; } = new();
        public List<int> ManagerScopes { get; } = new();
        public Task<PagedResult<TimeOffRequestListItem>> SearchAsync(int? employeeId, string? status, int page, int pageSize)
        {
            RequestScopes.Add(employeeId);
            SearchStatuses.Add(status);
            return Task.FromResult(new PagedResult<TimeOffRequestListItem>());
        }
        public Task<List<TimeOffRequestListItem>> GetRecentForEmployeeAsync(int employeeId)
        {
            RequestScopes.Add(employeeId);
            return Task.FromResult(new List<TimeOffRequestListItem>());
        }
        public Task<List<TimeOffRequestListItem>> GetOutOnAsync(DateTime date, int? employeeId = null)
        {
            AbsenceScopes.Add(employeeId);
            return Task.FromResult(new List<TimeOffRequestListItem> { new() { EmployeeId = employeeId ?? 8 }, new() { EmployeeId = employeeId ?? 8 } });
        }
        public Task<List<TimeOffBalanceRow>> GetBalancesAsync(int id, int year) { BalanceScopes.Add(id); return Task.FromResult(new List<TimeOffBalanceRow>()); }
        public Task<List<TimeOffRequestListItem>> GetPendingForManagerAsync(int id) { ManagerScopes.Add(id); return Task.FromResult(new List<TimeOffRequestListItem>()); }
        public Task<TimeOffRequestListItem?> GetAsync(int id) => throw new NotSupportedException();
        public Task<List<TimeOffSummaryRow>> GetSummaryAsync(int year, int? departmentId) => throw new NotSupportedException();
        public Task<List<TimeOffStatusCount>> GetStatusCountsAsync() => throw new NotSupportedException();
    }

    private sealed class EmployeesFake : IEmployeeRepository
    {
        public Task<EmployeeProfile?> GetProfileAsync(int id) => Task.FromResult<EmployeeProfile?>(new() { EmployeeId = id, FirstName = "کاربر", LastName = "آزمایشی" });
        public Task<PagedResult<EmployeeListItem>> SearchAsync(string? k, int? d, string? s, int p, int size) => throw new NotSupportedException();
        public Task<List<EmployeeListItem>> GetByIdsAsync(IReadOnlyCollection<int> ids) => throw new NotSupportedException();
        public Task<List<EmployeeLookup>> GetManagerCandidatesAsync(int? id = null) => throw new NotSupportedException();
        public Task<List<EmployeeJobHistoryRow>> GetJobHistoryAsync(int id) => throw new NotSupportedException();
        public Task<List<EmployeeLookup>> GetDirectReportsAsync(int id) => throw new NotSupportedException();
        public Task<EmployeeNeighbours?> GetNeighboursAsync(int id) => throw new NotSupportedException();
        public Task<string> NextEmployeeNumberAsync() => throw new NotSupportedException();
        public Task<bool> IsEmployeeNumberTakenAsync(string number, int? id = null) => throw new NotSupportedException();
        public Task<bool> IsEmailTakenAsync(string email, int? id = null) => throw new NotSupportedException();
    }
}
