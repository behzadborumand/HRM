namespace HRMS.Core.Entities
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public int? CompanyId { get; set; }
        public string? EmployeeNumber { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PreferredName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public DateTime HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public string EmploymentStatus { get; set; } = "Active";
        public string? AvatarPath { get; set; }
        public string? SSNEncrypted { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
    }
}
