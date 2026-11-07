using HRMS.Web.Security;
using HRMS.Web.Services;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _account;

        public AccountController(AccountService account)
        {
            _account = account;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["Title"] = "ورود به سامانه";
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["Title"] = "ورود به سامانه";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _account.ValidateAsync(model.Username, model.Password);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "نام کاربری یا رمزعبور اشتباه است، یا حساب شما غیرفعال شده است.");
                return View(model);
            }

            var principal = IdentityClaims.Create(user, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8))
                });

            TempData["Welcome"] = true;
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : Url.Action("Index", "Home")!);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Message"] = "از حساب خود خارج شدید.";
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = "دسترسی غیرمجاز";
            return View();
        }
    }
}
