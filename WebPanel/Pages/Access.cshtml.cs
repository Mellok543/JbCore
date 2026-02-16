using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class AccessModel(ExcelAdminService adminService) : PageModel
{
    public IReadOnlyList<UserAccessVm> Users { get; private set; } = [];
    public IReadOnlyList<RecommendationVm> PendingRecommendations { get; private set; } = [];

    [TempData]
    public string FlashMessage { get; set; } = string.Empty;

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPostReview(long id, string action)
    {
        var accept = string.Equals(action, "accept", StringComparison.OrdinalIgnoreCase);
        var reviewer = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            reviewer = "webpanel";
        }

        var result = adminService.ReviewRecommendation(id, accept, reviewer);
        FlashMessage = result.Message;

        return RedirectToPage();
    }

    private void LoadData()
    {
        Users = adminService.GetUsers();
        PendingRecommendations = adminService.GetPendingRecommendations();
    }
}
