namespace HRMS.Next.Modules.Documents;

public static class DocumentRules
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip"
    };

    public static string? Validate(string? title, string? category, string? fileName, long size)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            return "عنوان سند باید بین ۱ تا ۲۰۰ نویسه باشد.";
        if (category?.Trim().Length > 50)
            return "دسته‌بندی سند نباید بیش از ۵۰ نویسه باشد.";
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 260 ||
            fileName.Contains(':') || fileName.Any(char.IsControl) ||
            fileName.IndexOfAny(['/', '\\', '\r', '\n']) >= 0 ||
            !Extensions.Contains(Path.GetExtension(fileName)))
            return "نام یا پسوند فایل مجاز نیست.";
        if (size <= 0 || size > MaxFileSizeBytes)
            return "حجم فایل باید بین ۱ بایت و ۱۰ مگابایت باشد.";
        return null;
    }

    public static string SafeDownloadName(string fileName) => Path.GetFileName(fileName);
}
