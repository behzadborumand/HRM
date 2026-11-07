using System.ComponentModel.DataAnnotations;
using HRMS.Core.Dtos;
using HRMS.Core.Entities;

namespace HRMS.Web.ViewModels
{
    public class DepartmentRow
    {
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public int? ParentDepartmentId { get; init; }
        public string? ParentName { get; init; }
        public int Headcount { get; init; }
    }

    public class DepartmentAdminViewModel
    {
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "نام واحد سازمانی الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام واحد سازمانی")]
        public string DepartmentName { get; set; } = string.Empty;

        [Display(Name = "واحد بالادستی")]
        public int? ParentDepartmentId { get; set; }

        public List<DepartmentRow> Rows { get; set; } = new();
        public List<Department> Options { get; set; } = new();
        public bool IsEdit => DepartmentId.HasValue;
    }

    public class JobTitleRow
    {
        public int JobTitleId { get; init; }
        public string TitleName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int Headcount { get; init; }
    }

    public class JobTitleAdminViewModel
    {
        public int? JobTitleId { get; set; }

        [Required(ErrorMessage = "عنوان شغلی الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "عنوان شغلی")]
        public string TitleName { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "شرح نقش")]
        public string? Description { get; set; }

        public List<JobTitleRow> Rows { get; set; } = new();
        public bool IsEdit => JobTitleId.HasValue;
    }

    public class PolicyAdminViewModel
    {
        public int? PolicyId { get; set; }

        [Required(ErrorMessage = "نام سیاست الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام سیاست مرخصی")]
        public string PolicyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "دسته‌بندی الزامی است")]
        [Display(Name = "دسته‌بندی")]
        public string Category { get; set; } = "Vacation";

        [Range(0, 2000, ErrorMessage = "سقف ساعت سالانه نامعتبر است")]
        [Display(Name = "ساعت مجاز سالانه (۰ = بدون سقف)")]
        public decimal AnnualHours { get; set; }

        [StringLength(1000, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "فعال")]
        public bool Active { get; set; }

        public List<TimeOffPolicy> Policies { get; set; } = new();
        public bool IsEdit => PolicyId.HasValue;
    }

    public class UserAdminViewModel
    {
        public int? UserId { get; set; }

        [Required(ErrorMessage = "نام کاربری الزامی است")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام کاربری")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "کارمند الزامی است")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "نقش الزامی است")]
        [Display(Name = "نقش")]
        public int RoleId { get; set; }

        [StringLength(100, ErrorMessage = "رمزعبور حداکثر ۱۰۰ نویسه است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمزعبور (برای تغییر وارد شود)")]
        public string? Password { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; }

        public List<UserListItem> Users { get; set; } = new();
        public List<Role> Roles { get; set; } = new();
        public List<EmployeeLookup> Employees { get; set; } = new();
        public bool IsEdit => UserId.HasValue;
    }

    public class RoleAdminViewModel
    {
        public int? RoleId { get; set; }

        [Required(ErrorMessage = "نام نقش الزامی است")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام نقش")]
        public string RoleName { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "توضیح نقش")]
        public string? Description { get; set; }

        public List<string> AvailablePermissions { get; set; } = new();
        public List<string> SelectedPermissions { get; set; } = new();
        public Dictionary<string, string> PermissionLabels { get; set; } = new();
        public List<RoleRow> Rows { get; set; } = new();
        public bool IsEdit => RoleId.HasValue;
    }

    public class RoleRow
    {
        public int RoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public List<string> Permissions { get; init; } = new();
        public int UserCount { get; init; }
    }

    public class CompanyAdminViewModel
    {
        public int? CompanyId { get; set; }

        [Required(ErrorMessage = "نام شرکت الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام شرکت")]
        public string CompanyName { get; set; } = string.Empty;

        public List<Company> Companies { get; set; } = new();
        public bool IsEdit => CompanyId.HasValue;
    }
}
