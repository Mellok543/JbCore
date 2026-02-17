namespace WebPanel.Services;

public interface IAdminDataService
{
    DashboardVm GetDashboard();
    IReadOnlyList<RequestVm> GetRequests(string category, string status);
    ReviewResultVm CreateRequest(string category, string reporter, string unit, string description, string quantity, string note);
    IReadOnlyList<UserAccessVm> GetUsers();
    IReadOnlyList<RecommendationVm> GetPendingRecommendations();
    ReviewResultVm ReviewRecommendation(long recommendationId, bool accept, string reviewer);
    ReviewResultVm UpdateRequestStatus(string category, long requestId, bool completed);
    ReviewResultVm UpdateUserAccess(long userId, bool canUseBot, bool canComplete, bool canManageAccess, bool notifyRequests, bool notifyRecommendations);
    IReadOnlyList<SupportTicketVm> GetSupportTickets(int take = 50);
    ReviewResultVm AddSupportTicket(string author, string topic, string details);
    ReviewResultVm UpdateSupportTicketStatus(long ticketId, string status);
}
