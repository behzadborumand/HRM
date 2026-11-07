using System.ComponentModel.DataAnnotations;
using HRMS.Core.Entities;

namespace HRMS.Web.ViewModels
{
    public class DocumentUploadViewModel
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "عنوان سند الزامی است")]
        [StringLength(200, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "عنوان سند")]
        public string? Title { get; set; }

        [StringLength(50, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        [Display(Name = "دسته‌بندی")]
        public string? Category { get; set; }

        [Display(Name = "تاریخ انقضا")]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "فایل")]
        public IFormFile? File { get; set; }
    }

    public class EmployeeDocumentsViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public List<EmployeeDocument> Documents { get; set; } = new();
        public DocumentUploadViewModel Upload { get; set; } = new();
        public string AllowedExtensions { get; set; } = string.Empty;
        public string MaxSizeLabel { get; set; } = string.Empty;
    }
}
