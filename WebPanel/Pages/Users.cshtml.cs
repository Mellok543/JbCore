using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

[Authorize(Roles = "admin")]
public sealed class UsersModel(IAuthService authService) : PageModel
{
    public IReadOnlyList<SiteUserVm> Users { get; private set; } = [];

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;

    [BindProperty]
    public string Role { get; set; } = "operator";

    [TempData]
    public string FlashMessage { get; set; } = string.Empty;

    public void OnGet()
    {
        Users = authService.GetUsers();
    }

    public IActionResult OnPostCreate()
    {
        var result = authService.CreateUser(Username, Password, DisplayName, Role);
        FlashMessage = result.Message;
        return RedirectToPage();
    }
}
