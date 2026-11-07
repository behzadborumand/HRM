namespace HRMS.Core.Dtos;

public record DashboardPerson(int EmployeeId, string FullName, DateTime? DateOfBirth, DateTime HireDate, string? JobTitle, bool HasPhoto);
public record DashboardCelebration(DashboardPerson Person, DateTime Date, string Kind, int? Years);
