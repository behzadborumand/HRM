namespace HRMS.Core.Entities
{
    public class Department
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? ParentDepartmentId { get; set; }
    }
}
