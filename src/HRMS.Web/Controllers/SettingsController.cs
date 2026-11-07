using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Web.Security;
using HRMS.Web.Services;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IRepository<Department> _departments;
        private readonly IRepository<JobTitle> _jobTitles;
        private readonly IRepository<EmployeeJob> _jobs;
        private readonly IRepository<TimeOffPolicy> _policies;
        private readonly IRepository<TimeOffRequest> _requests;
        private readonly IRepository<TimeOffBalance> _balances;
        private readonly IRepository<Company> _companies;
        private readonly IRepository<Employee> _employees;
        private readonly IRepository<Role> _roles;
        private readonly IRepository<User> _userStore;
        private readonly IUserRepository _userRepository;
        private readonly AccountService _account;

        public SettingsController(
            IRepository<Department> departments,
            IRepository<JobTitle> jobTitles,
            IRepository<EmployeeJob> jobs,
            IRepository<TimeOffPolicy> policies,
            IRepository<TimeOffRequest> requests,
            IRepository<TimeOffBalance> balances,
            IRepository<Company> companies,
            IRepository<Employee> employees,
            IRepository<Role> roles,
            IRepository<User> userStore,
            IUserRepository userRepository,
            AccountService account)
        {
            _departments = departments;
            _jobTitles = jobTitles;
            _jobs = jobs;
            _policies = policies;
            _requests = requests;
            _balances = balances;
            _companies = companies;
            _employees = employees;
            _roles = roles;
            _userStore = userStore;
            _userRepository = userRepository;
            _account = account;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "تنظیمات سیستم";
            return View();
        }

        [Authorize(Policy = Policies.OrgRead)]
        public async Task<IActionResult> Departments(int? edit = null)
        {
            ViewData["Title"] = "واحدهای سازمانی";
            var model = new DepartmentAdminViewModel
            {
                Rows = await DepartmentRowsAsync(),
                Options = await ParentOptionsAsync()
            };

            if (edit is not null)
            {
                var row = model.Rows.FirstOrDefault(r => r.DepartmentId == edit);
                if (row is not null)
                {
                    model.DepartmentId = row.DepartmentId;
                    model.DepartmentName = row.DepartmentName;
                    model.ParentDepartmentId = row.ParentDepartmentId;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Departments(DepartmentAdminViewModel model)
        {
            model.Rows = await DepartmentRowsAsync();
            model.Options = await ParentOptionsAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var name = model.DepartmentName.Trim();
            var duplicate = await _departments.Query().AnyAsync(d =>
                d.DepartmentName == name && (model.DepartmentId == null || d.DepartmentId != model.DepartmentId));
            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.DepartmentName), "واحدی با این نام قبلاً ثبت شده است.");
                return View(model);
            }

            if (model.DepartmentId is not null && model.ParentDepartmentId == model.DepartmentId)
            {
                ModelState.AddModelError(nameof(model.ParentDepartmentId), "یک واحد نمی‌تواند بالادست خودش باشد.");
                return View(model);
            }

            if (model.DepartmentId is null)
            {
                await _departments.AddAsync(new Department { DepartmentName = name, ParentDepartmentId = model.ParentDepartmentId });
            }
            else
            {
                var department = await _departments.FindAsync(model.DepartmentId.Value);
                if (department is null)
                {
                    return NotFound();
                }

                department.DepartmentName = name;
                department.ParentDepartmentId = model.ParentDepartmentId;
                _departments.Update(department);
            }

            await _departments.SaveChangesAsync();
            TempData["Message"] = model.DepartmentId is null ? "واحد سازمانی افزوده شد." : "واحد سازمانی به‌روزرسانی شد.";
            return RedirectToAction(nameof(Departments));
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var inUse = await _jobs.Query().AnyAsync(j => j.DepartmentId == id);
            var hasChildren = await _departments.Query().AnyAsync(d => d.ParentDepartmentId == id);

            TempData[inUse || hasChildren ? "Error" : "Message"] = inUse
                ? "این واحد هنوز کارمند دارد و قابل حذف نیست."
                : hasChildren
                    ? "این واحد زیرمجموعه دارد و قابل حذف نیست."
                    : "واحد سازمانی حذف شد.";

            if (!inUse && !hasChildren)
            {
                var department = await _departments.FindAsync(id);
                if (department is not null)
                {
                    _departments.Remove(department);
                    await _departments.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Departments));
        }

        [Authorize(Policy = Policies.OrgRead)]
        public async Task<IActionResult> JobTitles(int? edit = null)
        {
            ViewData["Title"] = "عناوین شغلی";
            var model = new JobTitleAdminViewModel
            {
                Rows = await JobTitleRowsAsync()
            };

            if (edit is not null)
            {
                var row = model.Rows.FirstOrDefault(r => r.JobTitleId == edit);
                if (row is not null)
                {
                    model.JobTitleId = row.JobTitleId;
                    model.TitleName = row.TitleName;
                    model.Description = row.Description;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JobTitles(JobTitleAdminViewModel model)
        {
            model.Rows = await JobTitleRowsAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var name = model.TitleName.Trim();
            if (await _jobTitles.Query().AnyAsync(t => t.TitleName == name && (model.JobTitleId == null || t.JobTitleId != model.JobTitleId)))
            {
                ModelState.AddModelError(nameof(model.TitleName), "عنوان شغلی با این نام وجود دارد.");
                return View(model);
            }

            if (model.JobTitleId is null)
            {
                await _jobTitles.AddAsync(new JobTitle { TitleName = name, Description = model.Description });
            }
            else
            {
                var title = await _jobTitles.FindAsync(model.JobTitleId.Value);
                if (title is null)
                {
                    return NotFound();
                }

                title.TitleName = name;
                title.Description = model.Description;
                _jobTitles.Update(title);
            }

            await _jobTitles.SaveChangesAsync();
            TempData["Message"] = model.JobTitleId is null ? "عنوان شغلی افزوده شد." : "عنوان شغلی به‌روزرسانی شد.";
            return RedirectToAction(nameof(JobTitles));
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJobTitle(int id)
        {
            if (await _jobs.Query().AnyAsync(j => j.JobTitleId == id))
            {
                TempData["Error"] = "این عنوان شغلی به کارمندانی انتساب دارد و قابل حذف نیست.";
            }
            else
            {
                var title = await _jobTitles.FindAsync(id);
                if (title is not null)
                {
                    _jobTitles.Remove(title);
                    await _jobTitles.SaveChangesAsync();
                    TempData["Message"] = "عنوان شغلی حذف شد.";
                }
            }

            return RedirectToAction(nameof(JobTitles));
        }

        [Authorize(Policy = Policies.OrgRead)]
        public async Task<IActionResult> TimeOffPolicies(int? edit = null)
        {
            ViewData["Title"] = "سیاست‌های مرخصی";
            var model = new PolicyAdminViewModel { Active = true, Policies = await _policies.ListAsync() };

            if (edit is not null)
            {
                var policy = model.Policies.FirstOrDefault(p => p.PolicyId == edit);
                if (policy is not null)
                {
                    model.PolicyId = policy.PolicyId;
                    model.PolicyName = policy.PolicyName;
                    model.Category = policy.Category ?? "Vacation";
                    model.AnnualHours = policy.AnnualHours;
                    model.Description = policy.Description;
                    model.Active = policy.Active;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TimeOffPolicies(PolicyAdminViewModel model)
        {
            model.Policies = await _policies.ListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.PolicyId is null)
            {
                await _policies.AddAsync(new TimeOffPolicy
                {
                    PolicyName = model.PolicyName.Trim(),
                    Category = model.Category,
                    AnnualHours = model.AnnualHours,
                    Description = model.Description,
                    Active = model.Active,
                    CompanyId = await _companies.Query().Select(c => (int?)c.CompanyId).FirstOrDefaultAsync()
                });
            }
            else
            {
                var policy = await _policies.FindAsync(model.PolicyId.Value);
                if (policy is null)
                {
                    return NotFound();
                }

                var previousAnnualHours = policy.AnnualHours;
                policy.PolicyName = model.PolicyName.Trim();
                policy.Category = model.Category;
                policy.AnnualHours = model.AnnualHours;
                policy.Description = model.Description;
                policy.Active = model.Active;

                if (previousAnnualHours != policy.AnnualHours)
                {
                    await RecalculateBalancesAsync(policy);
                }

                _policies.Update(policy);
            }

            await _policies.SaveChangesAsync();
            TempData["Message"] = model.PolicyId is null ? "سیاست مرخصی افزوده شد." : "سیاست مرخصی به‌روزرسانی شد و مانده‌ها بازمحاسبه گردید.";
            return RedirectToAction(nameof(TimeOffPolicies));
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePolicy(int id)
        {
            if (await _requests.Query().AnyAsync(r => r.PolicyId == id) || await _balances.Query().AnyAsync(b => b.PolicyId == id))
            {
                TempData["Error"] = "این سیاست مرخصی سابقه درخواست یا مانده دارد؛ آن را غیرفعال کنید.";
            }
            else
            {
                var policy = await _policies.FindAsync(id);
                if (policy is not null)
                {
                    _policies.Remove(policy);
                    await _policies.SaveChangesAsync();
                    TempData["Message"] = "سیاست مرخصی حذف شد.";
                }
            }

            return RedirectToAction(nameof(TimeOffPolicies));
        }

        [Authorize(Policy = Policies.OrgRead)]
        public async Task<IActionResult> Companies(int? edit = null)
        {
            ViewData["Title"] = "شرکت‌ها";
            var model = new CompanyAdminViewModel { Companies = await _companies.ListAsync() };

            if (edit is not null)
            {
                var company = model.Companies.FirstOrDefault(c => c.CompanyId == edit);
                if (company is not null)
                {
                    model.CompanyId = company.CompanyId;
                    model.CompanyName = company.CompanyName;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Companies(CompanyAdminViewModel model)
        {
            model.Companies = await _companies.ListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.CompanyId is null)
            {
                await _companies.AddAsync(new Company { CompanyName = model.CompanyName.Trim(), CreatedDate = DateTime.Now });
            }
            else
            {
                var company = await _companies.FindAsync(model.CompanyId.Value);
                if (company is null)
                {
                    return NotFound();
                }

                company.CompanyName = model.CompanyName.Trim();
                _companies.Update(company);
            }

            await _companies.SaveChangesAsync();
            TempData["Message"] = "اطلاعات شرکت ذخیره شد.";
            return RedirectToAction(nameof(Companies));
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var inUse = await _employees.Query().AnyAsync(e => e.CompanyId == id)
                || await _policies.Query().AnyAsync(p => p.CompanyId == id);
            if (inUse)
            {
                TempData["Error"] = "این شرکت به کارکنان یا سیاست مرخصی متصل است؛ ابتدا اتصالات را بردارید.";
                return RedirectToAction(nameof(Companies));
            }

            var company = await _companies.FindAsync(id);
            if (company is null)
            {
                return NotFound();
            }

            _companies.Remove(company);
            await _companies.SaveChangesAsync();
            TempData["Message"] = "شرکت حذف شد.";
            return RedirectToAction(nameof(Companies));
        }

        [Authorize(Policy = Policies.UsersRead)]
        public async Task<IActionResult> Users(int? edit = null)
        {
            ViewData["Title"] = "مدیریت کاربران";
            var employees = await _employees.Query()
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new Core.Dtos.EmployeeLookup
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToListAsync();

            var model = new UserAdminViewModel
            {
                IsActive = true,
                Users = await _userRepository.ListAsync(),
                Roles = await _roles.ListAsync(),
                Employees = employees
            };

            if (edit is not null)
            {
                var user = model.Users.FirstOrDefault(u => u.UserId == edit);
                if (user is not null)
                {
                    model.UserId = user.UserId;
                    model.Username = user.Username;
                    model.EmployeeId = user.EmployeeId;
                    model.RoleId = user.RoleId;
                    model.IsActive = user.IsActive;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.UsersWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Users(UserAdminViewModel model)
        {
            var result = model.UserId is null
                ? await _account.CreateAsync(model.Username, model.EmployeeId, model.RoleId, model.Password ?? string.Empty, model.IsActive)
                : await _account.UpdateAsync(model.UserId.Value, model.Username, model.EmployeeId, model.RoleId, model.Password, model.IsActive);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                model.Users = await _userRepository.ListAsync();
                model.Roles = await _roles.ListAsync();
                model.Employees = await _employees.Query()
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .Select(e => new Core.Dtos.EmployeeLookup { EmployeeId = e.EmployeeId, FullName = e.FirstName + " " + e.LastName })
                    .ToListAsync();
                return View(model);
            }

            TempData["Message"] = model.UserId is null ? "حساب کاربری ساخته شد." : "حساب کاربری به‌روزرسانی شد.";
            return RedirectToAction(nameof(Users));
        }

        [Authorize(Policy = Policies.UsersWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (User.UserId() == id)
            {
                TempData["Error"] = "نمی‌توانید حساب کاربری خود را حذف کنید.";
            }
            else
            {
                var result = await _account.DeleteAsync(id);
                TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                    ? "حساب کاربری حذف شد."
                    : string.Join(" ", result.Errors);
            }

            return RedirectToAction(nameof(Users));
        }

        [Authorize(Policy = Policies.UsersRead)]
        public async Task<IActionResult> Roles(int? edit = null)
        {
            ViewData["Title"] = "نقش‌ها و دسترسی‌ها";

            var roles = await _roles.ListAsync();
            var userCountByRole = (await _userStore.ListAsync()).GroupBy(u => u.RoleId).ToDictionary(g => g.Key, g => g.Count());

            var rows = roles
                .OrderBy(r => r.RoleId)
                .Select(r => new RoleRow
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Description = r.Description,
                    Permissions = (r.Permissions ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList(),
                    UserCount = userCountByRole.TryGetValue(r.RoleId, out var count) ? count : 0
                })
                .ToList();

            var model = new RoleAdminViewModel
            {
                Rows = rows,
                AvailablePermissions = Permissions.All.ToList(),
                PermissionLabels = Permissions.Labels.ToDictionary(k => k.Key, v => v.Value),
                SelectedPermissions = new List<string>()
            };

            if (edit is not null)
            {
                var role = rows.FirstOrDefault(r => r.RoleId == edit);
                if (role is not null)
                {
                    model.RoleId = role.RoleId;
                    model.RoleName = role.RoleName;
                    model.Description = role.Description;
                    model.SelectedPermissions = role.Permissions;
                }
            }

            return View(model);
        }

        [Authorize(Policy = Policies.UsersWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Roles(RoleAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Roles), new { edit = model.RoleId });
            }

            var permissions = string.Join(",", (model.SelectedPermissions ?? new List<string>())
                .Intersect(Permissions.All)
                .OrderBy(p => p));

            if (model.RoleId is null)
            {
                await _roles.AddAsync(new Role
                {
                    RoleName = model.RoleName.Trim(),
                    Description = model.Description,
                    Permissions = permissions
                });
            }
            else
            {
                var role = await _roles.FindAsync(model.RoleId.Value);
                if (role is null)
                {
                    return NotFound();
                }

                role.RoleName = model.RoleName.Trim();
                role.Description = model.Description;
                role.Permissions = permissions;
                _roles.Update(role);
            }

            await _roles.SaveChangesAsync();
            TempData["Message"] = "نقش ذخیره شد. کاربران مربوطه با ورود بعدی دسترسی تازه می‌گیرند.";
            return RedirectToAction(nameof(Roles));
        }

        [Authorize(Policy = Policies.UsersWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var roleName = await _roles.Query().Where(r => r.RoleId == id).Select(r => r.RoleName).FirstOrDefaultAsync();
            if (roleName is null)
            {
                return RedirectToAction(nameof(Roles));
            }

            if (roleName is "Admin" or "Manager" or "Employee")
            {
                TempData["Error"] = "نقش‌های پیش‌ساخته قابل حذف نیستند.";
            }
            else if (await _userStore.Query().AnyAsync(u => u.RoleId == id))
            {
                TempData["Error"] = "این نقش به کاربران منتسب است؛ ابتدا نقش آن‌ها را تغییر دهید.";
            }
            else
            {
                var role = await _roles.FindAsync(id);
                if (role is not null)
                {
                    _roles.Remove(role);
                    await _roles.SaveChangesAsync();
                    TempData["Message"] = "نقش حذف شد.";
                }
            }

            return RedirectToAction(nameof(Roles));
        }

        private async Task<List<DepartmentRow>> DepartmentRowsAsync()
        {
            var departments = await _departments.ListAsync();
            var jobs = await _jobs.ListAsync();

            return departments
                .OrderBy(d => d.DepartmentName)
                .Select(d => new DepartmentRow
                {
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.DepartmentName,
                    ParentDepartmentId = d.ParentDepartmentId,
                    ParentName = departments.FirstOrDefault(p => p.DepartmentId == d.ParentDepartmentId)?.DepartmentName,
                    Headcount = jobs.Count(j => j.DepartmentId == d.DepartmentId && j.IsCurrent)
                })
                .ToList();
        }

        private Task<List<Department>> ParentOptionsAsync() => _departments.ListAsync();

        private async Task<List<JobTitleRow>> JobTitleRowsAsync()
        {
            var titles = await _jobTitles.ListAsync();
            var jobs = await _jobs.ListAsync();

            return titles
                .OrderBy(t => t.TitleName)
                .Select(t => new JobTitleRow
                {
                    JobTitleId = t.JobTitleId,
                    TitleName = t.TitleName,
                    Description = t.Description,
                    Headcount = jobs.Count(j => j.JobTitleId == t.JobTitleId && j.IsCurrent)
                })
                .ToList();
        }

        /// <summary>Re-derives accrued hours for the current year whenever a policy's annual ceiling changes.</summary>
        private async Task RecalculateBalancesAsync(TimeOffPolicy policy)
        {
            var year = DateTime.Now.Year;
            var affected = await _balances.Query()
                .Where(b => b.PolicyId == policy.PolicyId && b.Year == year)
                .ToListAsync();

            foreach (var balance in affected)
            {
                var hireDate = await _employees.Query()
                    .Where(e => e.EmployeeId == balance.EmployeeId)
                    .Select(e => e.HireDate)
                    .FirstOrDefaultAsync();

                balance.AccruedHours = Core.Services.TimeOffRules.ProrateAccrual(hireDate, year, policy.AnnualHours);
                balance.LastUpdated = DateTime.Now;
                _balances.Update(balance);
            }
        }
    }
}
