using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    public class TimeOffService
    {
        private readonly IRepository<TimeOffRequest> _requests;
        private readonly IRepository<TimeOffPolicy> _policies;
        private readonly IRepository<TimeOffBalance> _balances;
        private readonly IRepository<Employee> _employees;
        private readonly IRepository<EmployeeJob> _jobs;

        public TimeOffService(
            IRepository<TimeOffRequest> requests,
            IRepository<TimeOffPolicy> policies,
            IRepository<TimeOffBalance> balances,
            IRepository<Employee> employees,
            IRepository<EmployeeJob> jobs)
        {
            _requests = requests;
            _policies = policies;
            _balances = balances;
            _employees = employees;
            _jobs = jobs;
        }

        public async Task<OperationResult> CreateAsync(
            int employeeId, int policyId, DateTime startDate, DateTime endDate, string? reason, int requestedByEmployeeId)
        {
            var employee = await _employees.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند انتخاب‌شده یافت نشد.");
            }

            var policy = await _policies.FindAsync(policyId);
            if (policy is null || !policy.Active)
            {
                return OperationResult.Fail("سیاست مرخصی انتخاب‌شده فعال نیست.");
            }

            if (endDate < startDate)
            {
                return OperationResult.Fail("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
            }

            if (startDate.Date < employee.HireDate.Date)
            {
                return OperationResult.Fail("تاریخ شروع نمی‌تواند قبل از تاریخ استخدام باشد.");
            }

            var hours = TimeOffRules.WorkingHours(startDate, endDate);
            if (hours <= 0)
            {
                return OperationResult.Fail("بازه انتخابی فقط شامل تعطیلی است؛ حداقل یک روز کاری انتخاب کنید.");
            }

            var overlaps = await _requests.Query().AnyAsync(r =>
                r.EmployeeId == employeeId
                && r.Status != "Rejected"
                && r.Status != "Cancelled"
                && r.StartDate <= endDate && r.EndDate >= startDate);
            if (overlaps)
            {
                return OperationResult.Fail("برای این بازه، درخواست مرخصی دیگری ثبت شده است.");
            }

            if (policy.AnnualHours > 0)
            {
                var balance = await GetOrCreateBalanceAsync(employee, policy, startDate.Year);
                var pending = await _requests.Query()
                    .Where(r => r.EmployeeId == employeeId && r.PolicyId == policy.PolicyId
                                && r.Status == "Pending" && r.StartDate.Year == startDate.Year)
                    .SumAsync(r => r.TotalHours) ?? 0;

                if (balance.AccruedHours - balance.UsedHours - pending < hours)
                {
                    var available = Math.Max(0, balance.AccruedHours - balance.UsedHours - pending);
                    return OperationResult.Fail(
                        $"مانده مجاز {policy.PolicyName} برابر {available:0.##} ساعت است و درخواست {hours:0.##} ساعتی قابل ثبت نیست.");
                }
            }

            await _requests.AddAsync(new TimeOffRequest
            {
                EmployeeId = employeeId,
                PolicyId = policyId,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                TotalHours = hours,
                Reason = reason,
                Status = "Pending",
                RequestedBy = requestedByEmployeeId,
                RequestedDate = DateTime.Now
            });

            await _requests.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> ApproveAsync(int requestId, int approverEmployeeId, bool canApproveAll, string? comments)
        {
            var request = await _requests.FindAsync(requestId);
            if (request is null)
            {
                return OperationResult.Fail("درخواست مورد نظر یافت نشد.");
            }

            if (request.Status != "Pending")
            {
                return OperationResult.Fail("این درخواست قبلاً تعیین تکلیف شده است.");
            }

            if (!await CanApproveAsync(request, approverEmployeeId, canApproveAll))
            {
                return OperationResult.Fail("شما مدیر مستقیم این کارمند نیستید.");
            }

            var employee = await _employees.FindAsync(request.EmployeeId);
            var policy = await _policies.FindAsync(request.PolicyId);
            if (employee is null || policy is null)
            {
                return OperationResult.Fail("کارمند یا سیاست مرخصی این درخواست حذف شده است.");
            }

            var hours = request.TotalHours ?? TimeOffRules.WorkingHours(request.StartDate, request.EndDate);

            if (policy.AnnualHours > 0)
            {
                var balance = await GetOrCreateBalanceAsync(employee, policy, request.StartDate.Year);
                var otherPending = await _requests.Query()
                    .Where(r => r.EmployeeId == employee.EmployeeId && r.PolicyId == policy.PolicyId
                                && r.Status == "Pending" && r.RequestId != requestId && r.StartDate.Year == request.StartDate.Year)
                    .SumAsync(r => r.TotalHours) ?? 0;

                if (balance.AccruedHours - balance.UsedHours - otherPending < hours)
                {
                    return OperationResult.Fail(
                        $"مانده {policy.PolicyName} برای تایید این درخواست کافی نیست.");
                }

                balance.UsedHours += hours;
                balance.LastUpdated = DateTime.Now;
                _balances.Update(balance);
            }

            request.Status = "Approved";
            request.ApprovedBy = approverEmployeeId;
            request.ApprovedDate = DateTime.Now;
            request.Comments = comments;
            _requests.Update(request);

            await _requests.SaveChangesAsync();
            return OperationResult.Ok(requestId);
        }

        public async Task<OperationResult> RejectAsync(int requestId, int approverEmployeeId, bool canApproveAll, string? comments)
        {
            var request = await _requests.FindAsync(requestId);
            if (request is null)
            {
                return OperationResult.Fail("درخواست مورد نظر یافت نشد.");
            }

            if (request.Status != "Pending")
            {
                return OperationResult.Fail("این درخواست قبلاً تعیین تکلیف شده است.");
            }

            if (!await CanApproveAsync(request, approverEmployeeId, canApproveAll))
            {
                return OperationResult.Fail("شما مدیر مستقیم این کارمند نیستید.");
            }

            request.Status = "Rejected";
            request.ApprovedBy = approverEmployeeId;
            request.ApprovedDate = DateTime.Now;
            request.Comments = comments;
            _requests.Update(request);

            await _requests.SaveChangesAsync();
            return OperationResult.Ok(requestId);
        }

        public async Task<OperationResult> CancelAsync(int requestId, int employeeId)
        {
            var request = await _requests.FindAsync(requestId);
            if (request is null)
            {
                return OperationResult.Fail("درخواست مورد نظر یافت نشد.");
            }

            if (request.EmployeeId != employeeId)
            {
                return OperationResult.Fail("تنها ثبت‌کننده می‌تواند درخواست را لغو کند.");
            }

            if (request.Status != "Pending")
            {
                return OperationResult.Fail("فقط درخواست‌های در انتظار تایید قابل لغو هستند.");
            }

            request.Status = "Cancelled";
            _requests.Update(request);

            await _requests.SaveChangesAsync();
            return OperationResult.Ok(requestId);
        }

        public async Task<bool> IsManagerAsync(int employeeId, int managerEmployeeId) =>
            await _jobs.Query().AnyAsync(j => j.EmployeeId == employeeId && j.IsCurrent && j.ManagerId == managerEmployeeId);

        private async Task<bool> CanApproveAsync(TimeOffRequest request, int approverEmployeeId, bool canApproveAll) =>
            canApproveAll || request.EmployeeId != approverEmployeeId && await IsManagerAsync(request.EmployeeId, approverEmployeeId);

        private async Task<TimeOffBalance> GetOrCreateBalanceAsync(Employee employee, TimeOffPolicy policy, int year)
        {
            var balance = await _balances.Query().FirstOrDefaultAsync(b =>
                b.EmployeeId == employee.EmployeeId && b.PolicyId == policy.PolicyId && b.Year == year);

            if (balance is not null)
            {
                return balance;
            }

            balance = new TimeOffBalance
            {
                EmployeeId = employee.EmployeeId,
                PolicyId = policy.PolicyId,
                Year = year,
                AccruedHours = TimeOffRules.ProrateAccrual(employee.HireDate, year, policy.AnnualHours),
                UsedHours = 0,
                LastUpdated = DateTime.Now
            };
            await _balances.AddAsync(balance);
            await _balances.SaveChangesAsync();
            return balance;
        }
    }
}
