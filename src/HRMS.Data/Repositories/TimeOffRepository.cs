using HRMS.Core.Dtos;
using HRMS.Core.Paging;
using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class TimeOffRepository : ITimeOffRepository
    {
        private readonly HrmsDbContext _db;

        public TimeOffRepository(HrmsDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResult<TimeOffRequestListItem>> SearchAsync(int? employeeId, string? status, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

            var query = RequestRows();

            if (employeeId.HasValue)
            {
                query = query.Where(r => r.EmployeeId == employeeId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RequestId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TimeOffRequestListItem>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<TimeOffRequestListItem?> GetAsync(int requestId) =>
            await RequestRows().FirstOrDefaultAsync(r => r.RequestId == requestId);

        public async Task<List<TimeOffRequestListItem>> GetPendingForManagerAsync(int managerEmployeeId) =>
            await RequestRows()
                .Where(r => r.Status == "Pending" && _db.EmployeeJobs.Any(j =>
                    j.EmployeeId == r.EmployeeId && j.IsCurrent && j.ManagerId == managerEmployeeId))
                .OrderBy(r => r.StartDate)
                .ToListAsync();

        public async Task<List<TimeOffRequestListItem>> GetRecentForEmployeeAsync(int employeeId) =>
            await RequestRows().Where(r => r.EmployeeId == employeeId)
                .OrderByDescending(r => r.RequestedDate).ThenByDescending(r => r.RequestId)
                .Take(6).ToListAsync();

        public async Task<List<TimeOffRequestListItem>> GetOutOnAsync(DateTime date, int? employeeId = null) =>
            await RequestRows()
                .Where(r => r.Status == "Approved" && r.StartDate <= date && r.EndDate >= date && (employeeId == null || r.EmployeeId == employeeId))
                .OrderBy(r => r.EmployeeName)
                .ToListAsync();

        public async Task<List<TimeOffBalanceRow>> GetBalancesAsync(int employeeId, int year)
        {
            return await _db.TimeOffBalances.AsNoTracking()
                .Where(b => b.EmployeeId == employeeId && b.Year == year)
                .OrderBy(b => b.BalanceId)
                .Select(b => new TimeOffBalanceRow
                {
                    BalanceId = b.BalanceId,
                    PolicyId = b.PolicyId,
                    Year = b.Year,
                    AccruedHours = b.AccruedHours,
                    UsedHours = b.UsedHours,
                    PolicyName = _db.TimeOffPolicies.Where(p => p.PolicyId == b.PolicyId).Select(p => p.PolicyName).FirstOrDefault()!,
                    Category = _db.TimeOffPolicies.Where(p => p.PolicyId == b.PolicyId).Select(p => p.Category).FirstOrDefault(),
                    Unlimited = _db.TimeOffPolicies.Where(p => p.PolicyId == b.PolicyId).Select(p => p.AnnualHours).FirstOrDefault() == 0,
                    PendingHours = _db.TimeOffRequests
                        .Where(r => r.EmployeeId == b.EmployeeId && r.PolicyId == b.PolicyId && r.Status == "Pending" && r.StartDate.Year == year)
                        .Sum(r => r.TotalHours) ?? 0
                })
                .ToListAsync();
        }

        public async Task<List<TimeOffSummaryRow>> GetSummaryAsync(int year, int? departmentId)
        {
            var query = _db.TimeOffBalances.AsNoTracking().Where(b => b.Year == year);

            if (departmentId.HasValue)
            {
                query = query.Where(b => _db.EmployeeJobs.Any(j =>
                    j.EmployeeId == b.EmployeeId && j.IsCurrent && j.DepartmentId == departmentId));
            }

            return await query
                .OrderBy(b => b.EmployeeId)
                .Select(b => new TimeOffSummaryRow
                {
                    EmployeeId = b.EmployeeId,
                    EmployeeName = _db.Employees.Where(e => e.EmployeeId == b.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()!,
                    DepartmentName = _db.EmployeeJobs
                        .Where(j => j.EmployeeId == b.EmployeeId && j.IsCurrent)
                        .Select(j => _db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault())
                        .FirstOrDefault(),
                    PolicyName = _db.TimeOffPolicies.Where(p => p.PolicyId == b.PolicyId).Select(p => p.PolicyName).FirstOrDefault()!,
                    AccruedHours = b.AccruedHours,
                    UsedHours = b.UsedHours,
                    AvailableHours = b.AccruedHours - b.UsedHours,
                    RequestCount = _db.TimeOffRequests.Count(r =>
                        r.EmployeeId == b.EmployeeId && r.PolicyId == b.PolicyId && r.StartDate.Year == year)
                })
                .ToListAsync();
        }

        public async Task<List<TimeOffStatusCount>> GetStatusCountsAsync()
        {
            return await _db.TimeOffRequests.AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new TimeOffStatusCount
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Hours = g.Sum(r => r.TotalHours) ?? 0
                })
                .ToListAsync();
        }

        private IQueryable<TimeOffRequestListItem> RequestRows()
        {
            return from request in _db.TimeOffRequests.AsNoTracking()
                   join employee in _db.Employees on request.EmployeeId equals employee.EmployeeId
                   join policy in _db.TimeOffPolicies on request.PolicyId equals policy.PolicyId
                   join requester in _db.Employees on request.RequestedBy equals requester.EmployeeId
                   join approver in _db.Employees on request.ApprovedBy equals (int?)approver.EmployeeId into approverGroup
                   from approver in approverGroup.DefaultIfEmpty()
                   select new TimeOffRequestListItem
                   {
                       RequestId = request.RequestId,
                       EmployeeId = request.EmployeeId,
                       EmployeeName = employee.FirstName + " " + employee.LastName,
                       DepartmentName = _db.EmployeeJobs
                           .Where(j => j.EmployeeId == request.EmployeeId && j.IsCurrent)
                           .Select(j => _db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault())
                           .FirstOrDefault(),
                       PolicyId = request.PolicyId,
                       PolicyName = policy.PolicyName,
                       PolicyCategory = policy.Category,
                       StartDate = request.StartDate,
                       EndDate = request.EndDate,
                       TotalHours = request.TotalHours,
                       Reason = request.Reason,
                       Status = request.Status,
                       RequestedDate = request.RequestedDate,
                       RequestedBy = requester.FirstName + " " + requester.LastName,
                       ApprovedBy = approver == null ? null : approver.FirstName + " " + approver.LastName,
                       ApprovedDate = request.ApprovedDate,
                       Comments = request.Comments
                   };
        }
    }
}
