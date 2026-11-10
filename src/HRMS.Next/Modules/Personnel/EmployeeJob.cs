namespace HRMS.Next.Modules.Personnel;

public sealed class EmployeeJob
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ManagerEmployeeId { get; set; }
    public string? JobTitle { get; set; }
    public string? EmploymentType { get; set; }
    public string? PayType { get; set; }
    public decimal? PayRate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}