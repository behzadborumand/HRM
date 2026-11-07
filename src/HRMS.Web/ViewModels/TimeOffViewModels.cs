using System.ComponentModel.DataAnnotations;
using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Paging;

namespace HRMS.Web.ViewModels
{
    public class TimeOffRequestFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "کارمند الزامی است")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "نوع مرخصی الزامی است")]
        [Display(Name = "نوع مرخصی")]
        public int PolicyId { get; set; }

        [Required(ErrorMessage = "تاریخ شروع الزامی است")]
        [Display(Name = "از تاریخ")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "تاریخ پایان الزامی است")]
        [Display(Name = "تا تاریخ")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "توضیحات الزامی است")]
        [StringLength(2000, ErrorMessage = "توضیحات حداکثر ۲۰۰۰ نویسه است")]
        [Display(Name = "توضیحات")]
        public string Reason { get; set; } = string.Empty;

        public int EstimatedHours { get; set; }

        public bool CanChooseEmployee { get; set; }
        public List<EmployeeLookup> Employees { get; set; } = new();
        public List<TimeOffPolicy> Policies { get; set; } = new();
        public List<TimeOffBalanceRow> Balances { get; set; } = new();
    }

    public class TimeOffIndexViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public List<TimeOffBalanceRow> Balances { get; set; } = new();
        public PagedResult<TimeOffRequestListItem> Requests { get; set; } = new();
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Year { get; set; } = DateTime.Now.Year;
        public bool CanApprove { get; set; }
    }

    public class ApprovalQueueViewModel
    {
        public List<TimeOffRequestListItem> Pending { get; set; } = new();
        public bool SeeAll { get; set; }
    }

    public class DecisionViewModel
    {
        public int RequestId { get; set; }
        [Display(Name = "توضیح مدیر")]
        [StringLength(1000, ErrorMessage = "طول واردشده نباید بیش از {1} نویسه باشد")]
        public string? Comments { get; set; }
    }
}
