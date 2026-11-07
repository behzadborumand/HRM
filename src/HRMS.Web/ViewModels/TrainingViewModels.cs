using System.ComponentModel.DataAnnotations;
using HRMS.Core.Dtos;

namespace HRMS.Web.ViewModels
{
    public class TrainingIndexViewModel
    {
        public string? Keyword { get; set; }
        public int? CourseId { get; set; }
        public string? Status { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public List<TrainingAssignmentRow> Rows { get; set; } = new();
        public List<TrainingCourseRow> Courses { get; set; } = new();
        public List<EmployeeLookup> Employees { get; set; } = new();
        public bool CanWrite { get; set; }
        public TrainingAssignViewModel Form { get; set; } = new();
    }

    public class TrainingAssignViewModel
    {
        [Display(Name = "کارمند")]
        [Range(1, int.MaxValue, ErrorMessage = "کارمند را انتخاب کنید")]
        public int EmployeeId { get; set; }

        [Display(Name = "دوره آموزشی")]
        [Range(1, int.MaxValue, ErrorMessage = "دوره را انتخاب کنید")]
        public int CourseId { get; set; }

        [Display(Name = "تاریخ واگذاری")]
        public DateTime AssignedDate { get; set; } = DateTime.Today;

        [Display(Name = "سررسید")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "تاریخ انجام")]
        public DateTime? CompletedDate { get; set; }

        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "شماره مدرک")]
        public string? CertificateNumber { get; set; }

        [StringLength(500, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "توضیح")]
        public string? Notes { get; set; }
    }

    public class TrainingCompleteViewModel
    {
        public int AssignmentId { get; set; }
        public int EmployeeId { get; set; }

        [Display(Name = "تاریخ انجام")]
        public DateTime CompletedDate { get; set; } = DateTime.Today;

        [StringLength(100, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "شماره مدرک")]
        public string? CertificateNumber { get; set; }
    }

    public class TrainingCoursesViewModel
    {
        public List<TrainingCourseRow> Courses { get; set; } = new();
        public TrainingCourseFormViewModel Form { get; set; } = new();
    }

    public class TrainingCourseFormViewModel
    {
        public int? CourseId { get; set; }

        [Required(ErrorMessage = "عنوان دوره الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "عنوان دوره")]
        public string? Title { get; set; }

        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "دسته‌بندی")]
        public string? Category { get; set; }

        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "برگزارکننده")]
        public string? Provider { get; set; }

        [StringLength(1000, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "توضیح")]
        public string? Description { get; set; }

        [Display(Name = "دوره تکرار (روز)")]
        [Range(1, 3650, ErrorMessage = "دوره تکرار باید بین ۱ تا ۳۶۵۰ روز باشد")]
        public int? RenewalDays { get; set; }

        [Display(Name = "برای همه اجباری است")]
        public bool MandatoryForAll { get; set; }

        // جعبه تیک وقتی تیک نخورد چیزی پست نمی‌کند؛ مقدار پیش‌فرض در کنترلر ست می‌شود تا خالی بودن یعنی «غیرفعال».
        [Display(Name = "فعال")]
        public bool Active { get; set; }
    }
}
