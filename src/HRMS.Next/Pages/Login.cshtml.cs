using System.Security.Claims;
using HRMS.Next.Modules.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages;

public sealed class LoginModel(AccessService access) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var user = await access.ValidateAsync(Username.Trim(), Password, ct);
        if (user is null)
        {
            ModelState.AddModelError("", "نام کاربری یا رمز عبور نامعتبر است.");
            return Page();
        }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
