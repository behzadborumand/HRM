using HRMS.Core.Services;
using Xunit;

namespace HRMS.Tests
{
    public class PhotoRulesTests
    {
        private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly byte[] GifHeader = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0 };
        private static readonly byte[] WebpHeader = { 0x52, 0x49, 0x46, 0x46, 0x01, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 };

        [Theory]
        [InlineData("photo.png", "image/png")]
        [InlineData("photo.JPG", "image/jpeg")]
        [InlineData("photo.jpeg", "image/jpeg")]
        [InlineData("photo.gif", "image/gif")]
        [InlineData("photo.webp", "image/webp")]
        public void An_allowed_image_passes(string fileName, string contentType)
        {
            Assert.True(PhotoRules.IsAllowedExtension(fileName));
            Assert.Null(PhotoRules.Validate(fileName, 1024, PngHeader));
            Assert.Equal(contentType, PhotoRules.ContentTypeFor(fileName));
        }

        [Theory]
        [InlineData("payload.exe")]
        [InlineData("avatar.svg")]
        [InlineData("page.html")]
        [InlineData("no_extension")]
        public void An_unexpected_extension_is_rejected(string fileName)
        {
            Assert.False(PhotoRules.IsAllowedExtension(fileName));
            Assert.NotNull(PhotoRules.Validate(fileName, 1024, PngHeader));
        }

        [Theory]
        [MemberData(nameof(ImageHeaders))]
        public void Every_supported_format_signature_is_recognized(byte[] header)
        {
            Assert.True(PhotoRules.HasImageHeader(header));
        }

        public static TheoryData<byte[]> ImageHeaders() => new() { PngHeader, JpegHeader, GifHeader, WebpHeader };

        [Fact]
        public void A_renamed_document_is_rejected_even_though_it_looks_like_a_png()
        {
            var svg = System.Text.Encoding.ASCII.GetBytes("<svg onload=\"alert(1)\"></svg>");

            Assert.False(PhotoRules.HasImageHeader(svg));
            Assert.NotNull(PhotoRules.Validate("avatar.png", svg.Length, svg));
        }

        [Fact]
        public void A_truncated_header_is_not_an_image()
        {
            Assert.False(PhotoRules.HasImageHeader(new byte[] { 0x89, 0x50 }));
            Assert.False(PhotoRules.HasImageHeader(Array.Empty<byte>()));
        }

        [Fact]
        public void The_stored_extension_follows_the_bytes_and_not_the_filename()
        {
            var (extension, error) = PhotoRules.Inspect("sneaky.jpg", 1024, PngHeader);

            Assert.Null(error);
            Assert.Equal(".png", extension);
        }

        [Fact]
        public void The_size_ceiling_is_enforced()
        {
            Assert.NotNull(PhotoRules.Validate("big.png", PhotoRules.MaxPhotoBytes + 1, PngHeader));
            Assert.NotNull(PhotoRules.Validate("empty.png", 0, PngHeader));
            Assert.Null(PhotoRules.Validate("ok.png", PhotoRules.MaxPhotoBytes, PngHeader));
        }

        [Fact]
        public void An_unknown_extension_has_no_content_type()
        {
            Assert.Null(PhotoRules.ContentTypeFor("note.txt"));
            Assert.Null(PhotoRules.ContentTypeFor(null));
        }
    }
}
