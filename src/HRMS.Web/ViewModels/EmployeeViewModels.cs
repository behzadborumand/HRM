using System.ComponentModel.DataAnnotations;
using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Paging;

namespace HRMS.Web.ViewModels
{
    public class EmployeeFormViewModel
    {
        public int EmployeeId { get; set; }

        [Display(Name = "کد پرسنلی")]
        [StringLength(50, ErrorMessage = "کد پرسنلی حداکثر ۵۰ نویسه است")]
        public string? EmployeeNumber { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام خانوادگی الزامی است")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "نام خانوادگی")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "نام مستعار")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? PreferredName { get; set; }

        [EmailAddress(ErrorMessage = "ایمیل نامعتبر است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "ایمیل سازمانی")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "شماره تماس نامعتبر است")]
        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "شماره تماس")]
        public string? Phone { get; set; }

        [Display(Name = "تاریخ تولد")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "جنسیت")]
        public string? Gender { get; set; }

        [Display(Name = "وضعیت تاهل")]
        public string? MaritalStatus { get; set; }

        [Required(ErrorMessage = "تاریخ استخدام الزامی است")]
        [Display(Name = "تاریخ استخدام")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "وضعیت اشتغال الزامی است")]
        [Display(Name = "وضعیت اشتغال")]
        public string EmploymentStatus { get; set; } = "Active";

        [Display(Name = "تاریخ خروج")]
        [DataType(DataType.Date)]
        public DateTime? TerminationDate { get; set; }

        [Display(Name = "شرکت")]
        public int? CompanyId { get; set; }

        [Display(Name = "واحد سازمانی")]
        public int? DepartmentId { get; set; }

        [Display(Name = "عنوان شغلی")]
        public int? JobTitleId { get; set; }

        [Display(Name = "مدیر مستقیم")]
        public int? ManagerId { get; set; }

        [Display(Name = "نوع استخدام")]
        public string? EmploymentType { get; set; }

        [Display(Name = "نوع حقوق")]
        public string? PayType { get; set; }

        [Range(0, 1_000_000_000, ErrorMessage = "نرخ حقوق نامعتبر است")]
        [Display(Name = "نرخ حقوق")]
        public decimal? PayRate { get; set; }

        [Display(Name = "نشانی")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? AddressLine1 { get; set; }

        [Display(Name = "شهر")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? City { get; set; }

        [Display(Name = "استان")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? State { get; set; }

        [Display(Name = "کدپستی")]
        [StringLength(20, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? PostalCode { get; set; }

        [Display(Name = "کشور")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? Country { get; set; } = "ایران";

        [Display(Name = "تلفن منزل")]
        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? HomePhone { get; set; }

        [Display(Name = "تلفن همراه")]
        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? MobilePhone { get; set; }

        [Display(Name = "مخاطب اضطراری")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? EmergencyContactName { get; set; }

        [Display(Name = "تماس اضطراری")]
        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? EmergencyContactPhone { get; set; }

        [Display(Name = "نسابت")]
        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? EmergencyContactRelation { get; set; }

        public List<Department> Departments { get; set; } = new();
        public List<JobTitle> JobTitles { get; set; } = new();
        public List<EmployeeLookup> Managers { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public bool IsNew => EmployeeId == 0;
    }

    public class EmployeeIndexViewModel
    {
        public PagedResult<EmployeeListItem> Result { get; set; } = new();
        public string? Keyword { get; set; }
        public int? DepartmentId { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public List<Department> Departments { get; set; } = new();
    }

    public class EmployeeDetailsViewModel
    {
        public EmployeeProfile Profile { get; set; } = new();
        public List<TimeOffBalanceRow> Balances { get; set; } = new();
        public List<TimeOffRequestListItem> Requests { get; set; } = new();
        public List<EmployeeJobHistoryRow> JobHistory { get; set; } = new();
        public List<EmployeeLookup> DirectReports { get; set; } = new();
        public List<HRMS.Core.Entities.EmployeeDocument> Documents { get; set; } = new();
        public List<TrainingAssignmentRow> Trainings { get; set; } = new();
        public EmployeeNeighbours? Neighbours { get; set; }
    }

    public class BulkEditViewModel
    {
        public List<int> EmployeeIds { get; set; } = new();
        public List<EmployeeListItem> Employees { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<JobTitle> JobTitles { get; set; } = new();
        public List<EmployeeLookup> Managers { get; set; } = new();

        [Display(Name = "واحد سازمانی")]
        public int? DepartmentId { get; set; }

        [Display(Name = "عنوان شغلی")]
        public int? JobTitleId { get; set; }

        [Display(Name = "مدیر مستقیم")]
        public int? ManagerId { get; set; }

        [Display(Name = "نوع استخدام")]
        public string? EmploymentType { get; set; }

        [Display(Name = "وضعیت اشتغال")]
        public string? EmploymentStatus { get; set; }

        [Display(Name = "تاریخ تغییر")]
        public DateTime ChangeDate { get; set; } = DateTime.Today;
    }
}
