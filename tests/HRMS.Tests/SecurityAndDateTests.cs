using HRMS.Core.Security;
using HRMS.Web.Helpers;
using Xunit;

namespace HRMS.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void A_stored_hash_accepts_the_matching_password()
        {
            var stored = PasswordHasher.Hash("Admin@1234");

            Assert.True(PasswordHasher.Verify("Admin@1234", stored));
        }

        [Fact]
        public void A_stored_hash_rejects_another_password() =>
            Assert.False(PasswordHasher.Verify("admin1234", PasswordHasher.Hash("Admin@1234")));

        [Fact]
        public void The_same_password_hashes_differ_so_nothing_leaks_through_equals()
        {
            Assert.NotEqual(PasswordHasher.Hash("Admin@1234"), PasswordHasher.Hash("Admin@1234"));
        }

        [Fact]
        public void A_hash_keeps_the_algorithm_marker_and_iteration_count()
        {
            var parts = PasswordHasher.Hash("Admin@1234").Split('$');

            Assert.Equal("PBKDF2", parts[0]);
            Assert.Equal(100_000, int.Parse(parts[1]));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-hash")]
        [InlineData("PBKDF2$100000$notbase64$also-not-base64")]
        [InlineData("SCRYPT$100000$c2FsdA==$aGFzaA==")]
        public void Anything_that_is_not_a_pbkdf2_hash_fails_closed(string stored) =>
            Assert.False(PasswordHasher.Verify("Admin@1234", stored));
    }

    public class PersianDateTests
    {
        [Theory]
        [InlineData("1405/04/01")]
        [InlineData("۱۴۰۵/۰۴/۰۱")]
        [InlineData("1405-4-1")]
        [InlineData(" 1405/04/01 ")]
        public void A_jalali_date_is_read_in_any_of_the_shapes_users_type(string value)
        {
            Assert.True(PersianDate.TryParse(value, out var date));
            Assert.Equal(new DateTime(2026, 6, 22), date);
        }

        [Fact]
        public void An_iso_gregorian_date_is_read_as_gregorian()
        {
            Assert.True(PersianDate.TryParse("2026-06-22", out var date));
            Assert.Equal(new DateTime(2026, 6, 22), date);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("1405/13/01")] // month 13 does not exist
        [InlineData("1405/11/31")] // Bahman has thirty days
        [InlineData("1405/04")]
        [InlineData("next friday")]
        public void Anything_unparseable_is_rejected(string value) =>
            Assert.False(PersianDate.TryParse(value, out _));

        [Fact]
        public void A_day_is_rendered_with_the_jalali_calendar() =>
            Assert.Equal("1405/04/01", PersianDate.ToJalali(new DateTime(2026, 6, 22)));

        [Fact]
        public void A_missing_date_renders_as_an_empty_input_value()
        {
            Assert.Equal(string.Empty, PersianDate.ToJalaliInput(null));
            Assert.Equal(string.Empty, PersianDate.ToJalaliInput(default));
        }

        [Fact]
        public void Rendering_and_parsing_round_trip()
        {
            var jalali = PersianDate.ToJalali(new DateTime(2026, 11, 3));

            Assert.True(PersianDate.TryParse(jalali, out var parsed));
            Assert.Equal(new DateTime(2026, 11, 3), parsed);
        }

        [Fact]
        public void A_service_length_is_counted_in_jalali_years_months_and_days() =>
            Assert.Equal("۳ سال - ۵ ماه - ۹ روز",
                PersianDate.ToJalaliDuration(new DateTime(2021, 3, 21), new DateTime(2024, 8, 31)));

        [Fact]
        public void A_service_length_of_nothing_is_shown_as_zero_days() =>
            Assert.Equal("۰ روز", PersianDate.ToJalaliDuration(new DateTime(2021, 3, 21), new DateTime(2021, 3, 21)));

        [Fact]
        public void A_service_length_before_the_start_is_not_shown() =>
            Assert.Equal("-", PersianDate.ToJalaliDuration(new DateTime(2024, 8, 31), new DateTime(2021, 3, 21)));
    }
}
