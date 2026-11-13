using System.ComponentModel.DataAnnotations;
using HRMS.Next.Modules.Personnel;
namespace HRMS.Next.Pages.Employees;
public sealed class EmploymentStatusForm
{
    [Required(ErrorMessage="وضعیت اشتغال را انتخاب کنید.")]
    public string Status {get;set;}="";
    [Required(ErrorMessage="تاریخ مؤثر الزامی است.")]
    public string EffectiveDateJalali {get;set;}="";
    [Required(ErrorMessage="نسخهٔ پرونده نامعتبر است.")]
    public string Version {get;set;}="";
    public static EmploymentStatusForm From(Employee employee) => new()
    {
        Status=employee.IsOnLeave?"OnLeave":"Active", Version=Convert.ToBase64String(employee.RowVersion),
        EffectiveDateJalali=JalaliDate.Format(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime))
    };
}