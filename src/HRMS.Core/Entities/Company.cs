namespace HRMS.Core.Entities
{
    public class Company
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? LogoPath { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
