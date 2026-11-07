namespace HRMS.Core.Entities;

public class OrganizationLink
{
    public int LinkId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; }
}
