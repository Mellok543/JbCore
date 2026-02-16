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

    public IReadOnlyList<RequestVm> Requests { get; private set; } = [];

    public void OnGet()
    {
        Category = NormalizeCategory(Category);
        Status = Status == "completed" ? "completed" : "active";
        Requests = adminService.GetRequests(Category, Status);
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
