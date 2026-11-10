using System.ComponentModel.DataAnnotations;
using HRMS.Next.Modules.Personnel;

namespace HRMS.Next.Pages.Employees;

public sealed class JobChangeForm
{
    [Range(1, int.MaxValue, ErrorMessage = "واحد سازمانی را انتخاب کنید.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "عنوان شغلی الزامی است.")]
    [StringLength(200, ErrorMessage = "عنوان شغلی طولانی است.")]
    public string JobTitle { get; set; } = "";

    public int? ManagerEmployeeId { get; set; }

    [Required(ErrorMessage = "نوع استخدام الزامی است.")]
    public string EmploymentType { get; set; } = "";

    [Required(ErrorMessage = "تاریخ مؤثر الزامی است.")]
    public string EffectiveDateJalali { get; set; } = "";

    [Required(ErrorMessage = "نسخه پرونده نامعتبر است.")]
    public string Version { get; set; } = "";

    public JobChangeInput ToInput()
    {
        if (!JalaliDate.TryParse(EffectiveDateJalali, out var date))
            throw new ArgumentException("تاریخ مؤثر شمسی معتبر نیست.");
        return new JobChangeInput(DepartmentId, JobTitle, ManagerEmployeeId, EmploymentType, date);
    }

    public static JobChangeForm From(Employee employee, JobHistoryRow? current)
    {
        var iranToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        return new JobChangeForm
        {
            DepartmentId = current?.DepartmentId ?? employee.DepartmentId,
            JobTitle = current?.JobTitle ?? "",
            ManagerEmployeeId = current?.ManagerEmployeeId,
            EmploymentType = current?.EmploymentType ?? "",
            EffectiveDateJalali = JalaliDate.Format(iranToday),
            Version = Convert.ToBase64String(employee.RowVersion)
        };
    }
}