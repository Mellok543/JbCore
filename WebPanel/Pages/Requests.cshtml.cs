using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class RequestsModel(IAdminDataService adminService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Category { get; set; } = "drone";

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "active";

    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    [TempData]
    public string FlashMessage { get; set; } = string.Empty;

    public IReadOnlyList<RequestVm> Requests { get; private set; } = [];

    public void OnGet()
    {
        Category = NormalizeCategory(Category);
        Status = Status == "completed" ? "completed" : "active";

        var source = adminService.GetRequests(Category, Status);
        if (string.IsNullOrWhiteSpace(Query))
        {
            Requests = source;
            return;
        }

        var q = Query.Trim();
        Requests = source.Where(x =>
                x.Id.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Reporter.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Unit.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Note.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IActionResult OnPostToggleStatus(long id, string category, string status, string query)
    {
        var normalizedCategory = NormalizeCategory(category);
        var completed = !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
        var result = adminService.UpdateRequestStatus(normalizedCategory, id, completed);
        FlashMessage = result.Message;

        return RedirectToPage(new
        {
            Category = normalizedCategory,
            Status = status == "completed" ? "completed" : "active",
            Query = query ?? string.Empty
        });
    }

    public static string NormalizeCategory(string category)
    {
        return category switch
        {
            "repair" => "repair",
            "consumables" => "consumables",
            _ => "drone"
        };
    }
}
