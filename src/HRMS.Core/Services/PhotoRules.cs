namespace HRMS.Core.Services
{
    /// <summary>Which images the employee photo slot accepts, and what they are served back as.</summary>
    public static class PhotoRules
    {
        public const long MaxPhotoBytes = 2 * 1024 * 1024;

        /// <summary>Bytes read from the start of the upload to check that it really is an image.</summary>
        public const int HeaderBytes = 12;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public static bool IsAllowedExtension(string? fileName) =>
            fileName is not null && AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

        public static string ExtensionList() =>
            string.Join(", ", AllowedExtensions.Select(e => e.TrimStart('.')));

        public static string? ContentTypeFor(string? fileName) =>
            Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => null
            };

        /// <summary>
        /// The extension only names the file, so an image is accepted only when its first bytes match a
        /// known signature. A renamed SVG or HTML document would otherwise be handed back to the browser
        /// as a picture and could run in the site's own origin.
        /// </summary>
        public static bool HasImageHeader(ReadOnlySpan<byte> header) => DetectExtension(header) is not null;

        /// <summary>
        /// The image format the bytes actually are in, so a file is never stored and served under an
        /// extension that contradicts its content (a PNG renamed to .jpg would claim to be a JPEG).
        /// </summary>
        public static string? DetectExtension(ReadOnlySpan<byte> header)
        {
            if (StartsByteWith(header, 0xFF, 0xD8, 0xFF))
            {
                return ".jpg";
            }

            if (StartsByteWith(header, 0x89, 0x50, 0x4E, 0x47))
            {
                return ".png";
            }

            if (StartsByteWith(header, 0x47, 0x49, 0x46, 0x38))
            {
                return ".gif";
            }

            if (header.Length >= HeaderBytes && StartsTextWith(header, "RIFF") && BytesMatch(header, 8, "WEBP"))
            {
                return ".webp";
            }

            return null;
        }

        /// <summary>
        /// <param name="fileName">Name as the browser reported it.</param>
        /// <param name="fileSize">Bytes the browser claimed to send.</param>
        /// <param name="header">The first <see cref="HeaderBytes"/> bytes of the file.</param>
        /// </summary>
        public static string? Validate(string? fileName, long fileSize, ReadOnlySpan<byte> header) =>
            Inspect(fileName, fileSize, header).Error;

        /// <summary>
        /// Rejects anything that is not a supported image and names the extension the stored file must use,
        /// which comes from the bytes rather than from the uploaded filename.
        /// </summary>
        public static (string? Extension, string? Error) Inspect(string? fileName, long fileSize, ReadOnlySpan<byte> header)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (null, "فایل انتخاب نشده است.");
            }

            if (!IsAllowedExtension(fileName))
            {
                return (null, $"پسوند تصویر مجاز نیست؛ پسوندهای مجاز: {ExtensionList()}");
            }

            if (fileSize <= 0)
            {
                return (null, "فایل انتخابشده خالی است.");
            }

            if (fileSize > MaxPhotoBytes)
            {
                return (null, $"حجم تصویر نباید بیش از {MaxPhotoBytes / (1024 * 1024)} مگابایت باشد.");
            }

            var extension = DetectExtension(header);
            return extension is null
                ? (null, "محتوای فایل یک تصویر JPEG، PNG، GIF یا WebP نیست.")
                : (extension, null);
        }

        private static bool StartsByteWith(ReadOnlySpan<byte> header, params int[] signature)
        {
            if (header.Length < signature.Length)
            {
                return false;
            }

            for (var i = 0; i < signature.Length; i++)
            {
                if (header[i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsTextWith(ReadOnlySpan<byte> header, string text) =>
            BytesMatch(header, 0, text);

        private static bool BytesMatch(ReadOnlySpan<byte> header, int offset, string text)
        {
            if (header.Length < offset + text.Length)
            {
                return false;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (header[offset + i] != (byte)text[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
