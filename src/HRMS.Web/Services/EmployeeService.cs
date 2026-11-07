using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Helpers;
using HRMS.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    public class EmployeeService
    {
        private readonly IEmployeeRepository _employees;
        private readonly IRepository<Employee> _employeeStore;
        private readonly IRepository<EmployeeJob> _jobStore;
        private readonly IRepository<EmployeePersonalInfo> _personalStore;
        private readonly IRepository<TimeOffPolicy> _policyStore;
        private readonly IRepository<TimeOffBalance> _balanceStore;
        private readonly IRepository<TimeOffRequest> _requestStore;
        private readonly IRepository<User> _userStore;
        private readonly IRepository<EmployeeDocument> _documentStore;
        private readonly IRepository<TrainingAssignment> _trainingStore;
        private readonly EmployeePhotoService _photos;

        public EmployeeService(
            IEmployeeRepository employees,
            IRepository<Employee> employeeStore,
            IRepository<EmployeeJob> jobStore,
            IRepository<EmployeePersonalInfo> personalStore,
            IRepository<TimeOffPolicy> policyStore,
            IRepository<TimeOffBalance> balanceStore,
            IRepository<TimeOffRequest> requestStore,
            IRepository<User> userStore,
            IRepository<EmployeeDocument> documentStore,
            IRepository<TrainingAssignment> trainingStore,
            EmployeePhotoService photos)
        {
            _employees = employees;
            _employeeStore = employeeStore;
            _jobStore = jobStore;
            _personalStore = personalStore;
            _policyStore = policyStore;
            _balanceStore = balanceStore;
            _requestStore = requestStore;
            _userStore = userStore;
            _documentStore = documentStore;
            _trainingStore = trainingStore;
            _photos = photos;
        }

        public async Task<OperationResult> CreateAsync(EmployeeFormViewModel form)
        {
            var number = string.IsNullOrWhiteSpace(form.EmployeeNumber)
                ? await _employees.NextEmployeeNumberAsync()
                : form.EmployeeNumber.Trim();

            if (await _employees.IsEmployeeNumberTakenAsync(number))
            {
                return OperationResult.Fail($"کد پرسنلی {number} قبلاً ثبت شده است.");
            }

            if (!string.IsNullOrWhiteSpace(form.Email) && await _employees.IsEmailTakenAsync(form.Email.Trim()))
            {
                return OperationResult.Fail($"ایمیل {form.Email} برای کارمند دیگری استفاده شده است.");
            }

            var employee = new Employee();
            ApplyEmployee(employee, form);
            employee.EmployeeNumber = number;
            await _employeeStore.AddAsync(employee);
            await _employeeStore.SaveChangesAsync();

            await SaveJobAsync(employee, form, job: null);
            await SavePersonalAsync(employee.EmployeeId, form, existing: null);
            await OpenYearBalancesAsync(employee);

            await _employeeStore.SaveChangesAsync();

            return OperationResult.Ok(employee.EmployeeId);
        }

        public async Task<OperationResult> UpdateAsync(int employeeId, EmployeeFormViewModel form)
        {
            var employee = await _employeeStore.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند مورد نظر یافت نشد.");
            }

            var number = string.IsNullOrWhiteSpace(form.EmployeeNumber) ? employee.EmployeeNumber : form.EmployeeNumber.Trim();

            if (!string.IsNullOrWhiteSpace(number) && await _employees.IsEmployeeNumberTakenAsync(number, employeeId))
            {
                return OperationResult.Fail($"کد پرسنلی {number} قبلاً ثبت شده است.");
            }

            if (!string.IsNullOrWhiteSpace(form.Email) && await _employees.IsEmailTakenAsync(form.Email.Trim(), employeeId))
            {
                return OperationResult.Fail($"ایمیل {form.Email} برای کارمند دیگری استفاده شده است.");
            }

            ApplyEmployee(employee, form);
            employee.EmployeeNumber = number;
            employee.ModifiedDate = DateTime.Now;
            _employeeStore.Update(employee);

            var job = await _jobStore.Query()
                .FirstOrDefaultAsync(j => j.EmployeeId == employeeId && j.IsCurrent);
            await SaveJobAsync(employee, form, job);
            await SavePersonalAsync(employeeId, form, existing: null);

            if (job is null)
            {
                await OpenYearBalancesAsync(employee);
            }

            await _employeeStore.SaveChangesAsync();
            return OperationResult.Ok(employeeId);
        }

        public async Task<OperationResult> TerminateAsync(int employeeId, DateTime terminationDate)
        {
            var employee = await _employeeStore.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند مورد نظر یافت نشد.");
            }

            if (terminationDate < employee.HireDate)
            {
                return OperationResult.Fail("تاریخ خروج نمی‌تواند قبل از تاریخ استخدام باشد.");
            }

            employee.EmploymentStatus = "Terminated";
            employee.TerminationDate = terminationDate;
            employee.ModifiedDate = DateTime.Now;
            _employeeStore.Update(employee);

            var job = await _jobStore.Query().FirstOrDefaultAsync(j => j.EmployeeId == employeeId && j.IsCurrent);
            if (job is not null)
            {
                job.IsCurrent = false;
                job.EndDate = terminationDate;
                _jobStore.Update(job);
            }

            await _employeeStore.SaveChangesAsync();
            return OperationResult.Ok(employeeId);
        }

        public async Task<OperationResult> DeleteAsync(int employeeId)
        {
            var employee = await _employeeStore.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند مورد نظر یافت نشد.");
            }

            if (await _requestStore.Query().AnyAsync(r => r.EmployeeId == employeeId))
            {
                return OperationResult.Fail("این کارمند سابقه مرخصی دارد؛ پیش از حذف، سوابق مرخصی او را حذف یا منتقل کنید.");
            }

            if (await _userStore.Query().AnyAsync(u => u.EmployeeId == employeeId))
            {
                return OperationResult.Fail("برای این کارمند حساب کاربری فعال است؛ ابتدا از مدیریت کاربران آن را حذف کنید.");
            }

            if (await _jobStore.Query().AnyAsync(j => j.ManagerId == employeeId))
            {
                return OperationResult.Fail("این کارمند مدیر مستقیم برخی کارکنان است؛ ابتدا تعریف مدیر را در پرونده آن کارکنان تغییر دهید.");
            }

            if (await _documentStore.Query().AnyAsync(d => d.EmployeeId == employeeId))
            {
                return OperationResult.Fail("این کارمند سند بارگذاری‌شده دارد؛ ابتدا مدارک او را حذف کنید تا فایل‌هایشان هم از دست نرود.");
            }

            if (await _trainingStore.Query().AnyAsync(a => a.EmployeeId == employeeId))
            {
                return OperationResult.Fail("این کارمند ثبت آموزشی دارد؛ ابتدا از صفحه آموزش‌ها ثبت‌های او را حذف کنید.");
            }

            var job = await _jobStore.Query().FirstOrDefaultAsync(j => j.EmployeeId == employeeId);
            if (job is not null)
            {
                _jobStore.Remove(job);
            }

            var personal = await _personalStore.Query().FirstOrDefaultAsync(p => p.EmployeeId == employeeId);
            if (personal is not null)
            {
                _personalStore.Remove(personal);
            }

            foreach (var balance in await _balanceStore.Query().Where(b => b.EmployeeId == employeeId).ToListAsync())
            {
                _balanceStore.Remove(balance);
            }

            // Dependent rows are saved first: the FKs are mapped as scalar properties only,
            // so EF Core cannot order the DELETE statements itself.
            await _employeeStore.SaveChangesAsync();

            _employeeStore.Remove(employee);
            await _employeeStore.SaveChangesAsync();

            // Only after the row is gone: if the delete fails the profile keeps pointing at its image.
            _photos.DeleteFile(employee);
            return OperationResult.Ok(employeeId);
        }

        public async Task<OperationResult> BulkUpdateAsync(BulkEditViewModel form)
        {
            var employeeIds = (form.EmployeeIds ?? new List<int>()).Distinct().ToArray();
            if (employeeIds.Length == 0)
            {
                return OperationResult.Fail("هیچ کارمندی برای ویرایش گروهی انتخاب نشده است.");
            }

            if (!BulkEditRules.HasAnyChange(form.DepartmentId, form.JobTitleId, form.ManagerId, form.EmploymentType,
                    form.EmploymentStatus))
            {
                return OperationResult.Fail("برای ویرایش گروهی دست‌کم یک فیلد را انتخاب کنید؛ فیلد خالی یعنی بدون تغییر.");
            }

            var employees = await _employeeStore.Query().Where(e => employeeIds.Contains(e.EmployeeId)).ToListAsync();
            if (employees.Count == 0)
            {
                return OperationResult.Fail("کارمندانی که انتخاب شده بودند یافت نشدند.");
            }

            // The most recent hire date is the binding constraint for the whole selection.
            var latestHire = employees.OrderByDescending(e => e.HireDate).First();
            if (form.ChangeDate < latestHire.HireDate)
            {
                return OperationResult.Fail($"تاریخ تغییر نمی‌تواند پیش از تاریخ استخدام باشد؛ {latestHire.FirstName} {latestHire.LastName} در {PersianDate.ToJalali(latestHire.HireDate)} استخدام شده است.");
            }

            var updated = 0;
            foreach (var employee in employees)
            {
                var changed = false;

                if (BulkEditRules.ChangesStatus(form.EmploymentStatus) && employee.EmploymentStatus != form.EmploymentStatus)
                {
                    employee.EmploymentStatus = form.EmploymentStatus!;
                    changed = true;
                }

                if (BulkEditRules.ChangesJob(form.DepartmentId, form.JobTitleId, form.ManagerId, form.EmploymentType)
                    && await ApplyJobChangeAsync(employee, form))
                {
                    changed = true;
                }

                if (changed)
                {
                    employee.ModifiedDate = DateTime.Now;
                    updated++;
                }
            }

            await _employeeStore.SaveChangesAsync();
            return OperationResult.Ok(updated);
        }

        /// <summary>Closes the current job row and opens a new one, so a bulk move stays in the history.</summary>
        private async Task<bool> ApplyJobChangeAsync(Employee employee, BulkEditViewModel form)
        {
            var job = await _jobStore.Query().FirstOrDefaultAsync(j => j.EmployeeId == employee.EmployeeId && j.IsCurrent);

            var managerId = BulkEditRules.ManagerFor(form.ManagerId, employee.EmployeeId) ?? job?.ManagerId;
            var departmentId = form.DepartmentId ?? job?.DepartmentId;
            var jobTitleId = form.JobTitleId ?? job?.JobTitleId;
            var employmentType = string.IsNullOrWhiteSpace(form.EmploymentType) ? job?.EmploymentType : form.EmploymentType;

            var isSameJob = job is not null && job.DepartmentId == departmentId && job.JobTitleId == jobTitleId
                && job.ManagerId == managerId && job.EmploymentType == employmentType;
            if (isSameJob)
            {
                return false;
            }

            if (job is not null)
            {
                job.IsCurrent = false;
                job.EndDate = form.ChangeDate;
                _jobStore.Update(job);
            }

            await _jobStore.AddAsync(new EmployeeJob
            {
                EmployeeId = employee.EmployeeId,
                DepartmentId = departmentId,
                JobTitleId = jobTitleId,
                ManagerId = managerId,
                EmploymentType = employmentType,
                PayType = job?.PayType,
                PayRate = job?.PayRate,
                StartDate = form.ChangeDate,
                IsCurrent = true
            });

            return true;
        }

        private async Task SaveJobAsync(Employee employee, EmployeeFormViewModel form, EmployeeJob? job)
        {
            var hasJobData = form.DepartmentId.HasValue || form.JobTitleId.HasValue || form.ManagerId.HasValue || form.PayRate.HasValue;
            if (!hasJobData && job is null)
            {
                return;
            }

            if (job is null)
            {
                var created = new EmployeeJob { EmployeeId = employee.EmployeeId, StartDate = form.HireDate };
                await _jobStore.AddAsync(created);
                ApplyJob(created, employee, form);
                return;
            }

            if (job.ManagerId == form.ManagerId && job.DepartmentId == form.DepartmentId && job.JobTitleId == form.JobTitleId
                && job.EmploymentType == form.EmploymentType && job.PayType == form.PayType && job.PayRate == form.PayRate)
            {
                return;
            }

            ApplyJob(job, employee, form);
            _jobStore.Update(job);
        }

        private static void ApplyJob(EmployeeJob job, Employee employee, EmployeeFormViewModel form)
        {
            job.DepartmentId = form.DepartmentId;
            job.JobTitleId = form.JobTitleId;
            job.ManagerId = form.ManagerId == employee.EmployeeId ? null : form.ManagerId;
            job.EmploymentType = form.EmploymentType;
            job.PayType = form.PayType;
            job.PayRate = form.PayRate;
            job.IsCurrent = true;
        }

        private async Task SavePersonalAsync(int employeeId, EmployeeFormViewModel form, EmployeePersonalInfo? existing)
        {
            var hasPersonalData = !string.IsNullOrWhiteSpace(form.AddressLine1) || !string.IsNullOrWhiteSpace(form.City)
                || !string.IsNullOrWhiteSpace(form.HomePhone) || !string.IsNullOrWhiteSpace(form.EmergencyContactName);

            existing ??= await _personalStore.Query().FirstOrDefaultAsync(p => p.EmployeeId == employeeId);

            if (existing is null)
            {
                if (!hasPersonalData)
                {
                    return;
                }

                var created = new EmployeePersonalInfo { EmployeeId = employeeId };
                await _personalStore.AddAsync(created);
                ApplyPersonal(created, form);
                return;
            }

            ApplyPersonal(existing, form);
            _personalStore.Update(existing);
        }

        private static void ApplyPersonal(EmployeePersonalInfo info, EmployeeFormViewModel form)
        {
            info.AddressLine1 = form.AddressLine1;
            info.City = form.City;
            info.State = form.State;
            info.PostalCode = form.PostalCode;
            info.Country = form.Country;
            info.HomePhone = form.HomePhone;
            info.MobilePhone = form.MobilePhone;
            info.EmergencyContactName = form.EmergencyContactName;
            info.EmergencyContactPhone = form.EmergencyContactPhone;
            info.EmergencyContactRelation = form.EmergencyContactRelation;
        }

        private async Task OpenYearBalancesAsync(Employee employee)
        {
            var year = DateTime.Now.Year;
            foreach (var policy in await _policyStore.Query().Where(p => p.Active).ToListAsync())
            {
                var exists = await _balanceStore.Query().AnyAsync(b =>
                    b.EmployeeId == employee.EmployeeId && b.PolicyId == policy.PolicyId && b.Year == year);
                if (exists)
                {
                    continue;
                }

                await _balanceStore.AddAsync(new TimeOffBalance
                {
                    EmployeeId = employee.EmployeeId,
                    PolicyId = policy.PolicyId,
                    Year = year,
                    AccruedHours = TimeOffRules.ProrateAccrual(employee.HireDate, year, policy.AnnualHours),
                    UsedHours = 0,
                    LastUpdated = DateTime.Now
                });
            }
        }

        private static void ApplyEmployee(Employee employee, EmployeeFormViewModel form)
        {
            employee.FirstName = form.FirstName.Trim();
            employee.LastName = form.LastName.Trim();
            employee.PreferredName = form.PreferredName?.Trim();
            employee.Email = form.Email?.Trim();
            employee.Phone = form.Phone?.Trim();
            employee.DateOfBirth = form.DateOfBirth;
            employee.Gender = form.Gender;
            employee.MaritalStatus = form.MaritalStatus;
            employee.HireDate = form.HireDate;
            employee.TerminationDate = form.TerminationDate;
            employee.EmploymentStatus = form.EmploymentStatus;
            employee.CompanyId = form.CompanyId ?? employee.CompanyId;
        }
    }
}
