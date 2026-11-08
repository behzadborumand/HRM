using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HRMS.Next.Modules.Personnel;

namespace HRMS.Next.Pages.Employees;

public sealed class EmployeeForm
{
    [Required(ErrorMessage = "شماره پرسنلی الزامی است.")]
    [StringLength(50, ErrorMessage = "شماره پرسنلی طولانی است.")]
    public string EmployeeNumber { get; set; } = "";

    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100, ErrorMessage = "نام طولانی است.")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [StringLength(100, ErrorMessage = "نام خانوادگی طولانی است.")]
    public string LastName { get; set; } = "";

    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    public string? Email { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "واحد سازمانی را انتخاب کنید.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "تاریخ استخدام الزامی است.")]
    public string HireDateJalali { get; set; } = "";

    public string? Version { get; set; }

    public EmployeeInput ToInput()
    {
        if (!JalaliDate.TryParse(HireDateJalali, out var date)) throw new ArgumentException("تاریخ استخدام شمسی معتبر نیست.");
        return new EmployeeInput(DepartmentId, EmployeeNumber, FirstName, LastName, Email, date);
    }

    public static EmployeeForm From(Employee employee) => new()
    {
        EmployeeNumber = employee.EmployeeNumber,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentId = employee.DepartmentId,
        HireDateJalali = JalaliDate.Format(employee.HireDate),
        Version = Convert.ToBase64String(employee.RowVersion)
    };
}

public static class JalaliDate
{
    private static readonly PersianCalendar Calendar = new();

    public static string Format(DateOnly date)
    {
        var value = date.ToDateTime(TimeOnly.MinValue);
        return $"{Calendar.GetYear(value):0000}/{Calendar.GetMonth(value):00}/{Calendar.GetDayOfMonth(value):00}";
    }

    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = string.Concat(text.Trim().Select(c => c is >= '۰' and <= '۹' ? (char)('0' + c - '۰') : c is >= '٠' and <= '٩' ? (char)('0' + c - '٠') : c));
        var parts = normalized.Split('/');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) || !int.TryParse(parts[2], out var day)) return false;
        try { date = DateOnly.FromDateTime(Calendar.ToDateTime(year, month, day, 0, 0, 0, 0)); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
