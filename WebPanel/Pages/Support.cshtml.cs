using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class SupportModel(IAdminDataService adminService, ITelegramNotifier telegramNotifier) : PageModel
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

    public async Task<IActionResult> OnPostCreate()
    {
        var author = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(author))
        {
            author = "webpanel";
        }

        var result = adminService.AddSupportTicket(author, Topic, Details);
        if (result.Success)
        {
            var ticketId = ExtractTicketId(result.Message);
            await telegramNotifier.NotifySupportTicketAsync(ticketId, author, Topic, Details, HttpContext.RequestAborted);
        }

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

    private static long ExtractTicketId(string message)
    {
        var m = Regex.Match(message ?? string.Empty, @"#(\d+)");
        return m.Success && long.TryParse(m.Groups[1].Value, out var id) ? id : 0;
    }
}
