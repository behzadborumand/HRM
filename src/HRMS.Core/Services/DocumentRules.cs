namespace HRMS.Core.Services
{
    /// <summary>Which uploaded files the document vault accepts.</summary>
    public static class DocumentRules
    {
        public const long MaxFileSizeBytes = 10 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip" };

        public static bool IsAllowedExtension(string? fileName) =>
            fileName is not null && AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

        public static string ExtensionList() =>
            string.Join(", ", AllowedExtensions.Select(e => e.TrimStart('.')));

        public static string? Validate(string? fileName, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "فایل انتخاب نشده است.";
            }

            if (!IsAllowedExtension(fileName))
            {
                return $"پسوند فایل مجاز نیست؛ پسوندهای مجاز: {ExtensionList()}";
            }

            if (fileSize <= 0)
            {
                return "فایل انتخابشده خالی است.";
            }

            if (fileSize > MaxFileSizeBytes)
            {
                return $"حجم فایل نباید بیش از {MaxFileSizeBytes / (1024 * 1024)} مگابایت باشد.";
            }

            return null;
        }
    }
}
