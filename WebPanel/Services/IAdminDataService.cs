namespace WebPanel.Services;

public interface IAdminDataService
{
    DashboardVm GetDashboard();
    IReadOnlyList<RequestVm> GetRequests(string category, string status);
    IReadOnlyList<UserAccessVm> GetUsers();
    IReadOnlyList<RecommendationVm> GetPendingRecommendations();
    ReviewResultVm ReviewRecommendation(long recommendationId, bool accept, string reviewer);
    IReadOnlyList<SupportTicketVm> GetSupportTickets(int take = 50);
    ReviewResultVm AddSupportTicket(string author, string topic, string details);
}
