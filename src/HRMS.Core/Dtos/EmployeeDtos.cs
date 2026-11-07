namespace HRMS.Core.Dtos
{
    public class EmployeeListItem
    {
        public int EmployeeId { get; init; }
        public string? EmployeeNumber { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public DateTime HireDate { get; init; }
        public string EmploymentStatus { get; init; } = "Active";
        public string? DepartmentName { get; init; }
        public string? JobTitleName { get; init; }
        public string? ManagerName { get; init; }
        public decimal? PayRate { get; init; }
        public string? AvatarPath { get; init; }
        public DateTime ModifiedDate { get; init; }
        public bool HasPhoto => !string.IsNullOrWhiteSpace(AvatarPath);
    }

    public class EmployeeLookup
    {
        public int EmployeeId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string? JobTitleName { get; init; }
    }

    public class EmployeeProfile
    {
        public int EmployeeId { get; init; }
        public string? EmployeeNumber { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public string? PreferredName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public string? Gender { get; init; }
        public string? MaritalStatus { get; init; }
        public DateTime HireDate { get; init; }
        public DateTime? TerminationDate { get; init; }
        public string EmploymentStatus { get; init; } = "Active";
        public string? AvatarPath { get; init; }
        public DateTime ModifiedDate { get; init; }
        public bool HasPhoto => !string.IsNullOrWhiteSpace(AvatarPath);
        public string? CompanyName { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public int? JobTitleId { get; init; }
        public string? JobTitleName { get; init; }
        public int? ManagerId { get; init; }
        public string? ManagerName { get; init; }
        public string? EmploymentType { get; init; }
        public string? PayType { get; init; }
        public decimal? PayRate { get; init; }
        public string? AddressLine1 { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? PostalCode { get; init; }
        public string? Country { get; init; }
        public string? HomePhone { get; init; }
        public string? MobilePhone { get; init; }
        public string? EmergencyContactName { get; init; }
        public string? EmergencyContactPhone { get; init; }
        public string? EmergencyContactRelation { get; init; }
    }

    /// <summary>یک ردیف از تاریخچه شغلی کارمند، همان چیزی که ویرایش گروهی می‌سازد.</summary>
    public class EmployeeJobHistoryRow
    {
        public int EmployeeJobId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public bool IsCurrent { get; init; }
        public string? DepartmentName { get; init; }
        public string? JobTitleName { get; init; }
        public string? ManagerName { get; init; }
        public string? EmploymentType { get; init; }
        public string? PayType { get; init; }
        public decimal? PayRate { get; init; }
    }

    /// <summary>جایگاه کارمند در ترتیب نام‌ها، برای ناوبری قبلی/بعدی صفحه پرونده.</summary>
    public class EmployeeNeighbours
    {
        public int? PreviousEmployeeId { get; init; }
        public int? NextEmployeeId { get; init; }
        public int Position { get; init; }
        public int Total { get; init; }
    }
}
