namespace HRMS.Core.Entities
{
    public class JobTitle
    {
        public int JobTitleId { get; set; }
        public string TitleName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
