using System.ComponentModel.DataAnnotations;

namespace HRMS.Web.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        [Display(Name = "نام کاربری")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمزعبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمزعبور")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "مرا به خاطر بسپار")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
