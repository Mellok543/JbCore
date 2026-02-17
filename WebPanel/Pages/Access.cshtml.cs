using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class AccessModel(IAdminDataService adminService) : PageModel
{
    public bool IsAdmin => User.IsInRole("admin");
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
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }
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

    public IActionResult OnPostSaveUser(long userId, bool canUseBot, bool canComplete, bool canManageAccess, bool notifyRequests, bool notifyRecommendations)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }
        var result = adminService.UpdateUserAccess(userId, canUseBot, canComplete, canManageAccess, notifyRequests, notifyRecommendations);
        FlashMessage = result.Message;
        return RedirectToPage();
    }

    private void LoadData()
    {
        Users = adminService.GetUsers();
        PendingRecommendations = adminService.GetPendingRecommendations();
    }
}
