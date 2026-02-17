using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

[AllowAnonymous]
public sealed class LoginModel(IAuthService authService) : PageModel
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
        var result = authService.ValidateCredentials(Username, Password);

        if (!result.Success || result.User is null)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(result.User.DisplayName) ? result.User.Username : result.User.DisplayName),
            new(ClaimTypes.NameIdentifier, result.User.Username),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(result.User.Role) ? "operator" : result.User.Role)
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
