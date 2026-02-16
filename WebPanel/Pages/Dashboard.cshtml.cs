using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPanel.Services;

namespace WebPanel.Pages;

public sealed class DashboardModel(ExcelAdminService adminService) : PageModel
{
    public DashboardVm Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<RequestVm> ActiveDroneRequests { get; private set; } = [];
    public IReadOnlyList<RequestVm> ActiveRepairRequests { get; private set; } = [];
    public IReadOnlyList<RequestVm> ActiveConsumablesRequests { get; private set; } = [];

    public void OnGet()
    {
        Dashboard = adminService.GetDashboard();
        ActiveDroneRequests = adminService.GetRequests("drone", "active").Take(5).ToList();
        ActiveRepairRequests = adminService.GetRequests("repair", "active").Take(5).ToList();
        ActiveConsumablesRequests = adminService.GetRequests("consumables", "active").Take(5).ToList();
    }
}
