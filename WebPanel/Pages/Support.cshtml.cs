using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class SupportModel(IAdminDataService adminService) : PageModel
{
    public bool IsAdmin => User.IsInRole("admin");
    public IReadOnlyList<SupportTicketVm> Tickets { get; private set; } = [];

    [BindProperty]
    public string Topic { get; set; } = string.Empty;

    [BindProperty]
    public string Details { get; set; } = string.Empty;

    [TempData]
    public string FlashMessage { get; set; } = string.Empty;

    public void OnGet()
    {
        Tickets = adminService.GetSupportTickets(30);
    }

    public IActionResult OnPostCreate()
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }
        var author = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(author))
        {
            author = "webpanel";
        }

        var result = adminService.AddSupportTicket(author, Topic, Details);
        FlashMessage = result.Message;
        return RedirectToPage();
    }

    public IActionResult OnPostSetStatus(long id, string status)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }
        var result = adminService.UpdateSupportTicketStatus(id, status);
        FlashMessage = result.Message;
        return RedirectToPage();
    }
}
