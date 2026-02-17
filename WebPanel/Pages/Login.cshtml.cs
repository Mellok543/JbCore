using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using WebPanel.Services;

namespace WebPanel.Pages;

[AllowAnonymous]
public sealed class LoginModel(IOptions<AuthOptions> authOptions) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var user = authOptions.Value.Users.FirstOrDefault(x =>
            string.Equals(x.Username, Username, StringComparison.OrdinalIgnoreCase) &&
            x.Password == Password);

        if (user is null)
        {
            ErrorMessage = "Неверный логин или пароль.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName),
            new(ClaimTypes.NameIdentifier, user.Username),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "operator" : user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
