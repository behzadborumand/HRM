namespace HRMS.Core.Entities
{
    public class EmployeeDocument
    {
        public int DocumentId { get; set; }
        public int EmployeeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public int? UploadedBy { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.Now;
        public DateTime? ExpiryDate { get; set; }
    }
}
