using HRMS.Core.Dtos;
using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class ReportingRepository : IReportingRepository
    {
        private readonly HrmsDbContext _db;

        public ReportingRepository(HrmsDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardStats> GetDashboardAsync()
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = new DateTime(now.Year, 1, 1);

            var employees = _db.Employees.AsNoTracking();

            var headcountByDepartment = await _db.EmployeeJobs.AsNoTracking()
                .Where(j => j.IsCurrent && j.DepartmentId != null)
                .GroupBy(j => j.DepartmentId)
                .Select(g => new
                {
                    DepartmentName = _db.Departments.Where(d => d.DepartmentId == g.Key).Select(d => d.DepartmentName).FirstOrDefault()!,
                    Headcount = g.Count(),
                    PayrollCost = g.Sum(j => j.PayRate) ?? 0
                })
                .OrderByDescending(x => x.Headcount)
                .ToListAsync();

            var statusBreakdown = await _db.TimeOffRequests.AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new TimeOffStatusCount
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Hours = g.Sum(r => r.TotalHours) ?? 0
                })
                .ToListAsync();

            var active = await employees.CountAsync(e => e.EmploymentStatus == "Active");
            var departmentTotals = headcountByDepartment.Select(x => x.Headcount).ToList();

            return new DashboardStats
            {
                TotalEmployees = await employees.CountAsync(),
                ActiveEmployees = active,
                OnLeaveEmployees = await employees.CountAsync(e => e.EmploymentStatus == "OnLeave"),
                PendingRequests = await _db.TimeOffRequests.CountAsync(r => r.Status == "Pending"),
                ApprovedRequestsThisMonth = await _db.TimeOffRequests.CountAsync(r => r.Status == "Approved" && r.ApprovedDate >= monthStart),
                NewHiresThisYear = await employees.CountAsync(e => e.HireDate >= yearStart),
                DepartmentCount = await _db.Departments.CountAsync(),
                AverageTeamSize = departmentTotals.Count == 0 ? 0 : (decimal)Math.Round(departmentTotals.Average(), 1),
                HeadcountByDepartment = headcountByDepartment
                    .Select(x => new DepartmentHeadcount
                    {
                        DepartmentName = x.DepartmentName,
                        Headcount = x.Headcount,
                        PayrollCost = x.PayrollCost
                    })
                    .ToList(),
                RequestStatusBreakdown = statusBreakdown
            };
        }
    }
}
