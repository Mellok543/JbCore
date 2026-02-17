namespace WebPanel.Services;

public sealed record DashboardVm(
    int DroneActive,
    int DroneCompleted,
    int RepairActive,
    int RepairCompleted,
    int ConsumablesActive,
    int ConsumablesCompleted,
    int PendingRecommendations
);

public sealed record RequestVm(
    long Id,
    string Category,
    string Date,
    string Reporter,
    string Unit,
    string Description,
    string Note,
    string Status
);

public sealed record UserAccessVm(
    long UserId,
    string Username,
    string DisplayName,
    bool CanUseBot,
    bool CanComplete,
    bool CanManageAccess,
    bool NotifyRequests,
    bool NotifyRecommendations
);

public sealed record RecommendationVm(
    long Id,
    string Date,
    string Recommender,
    string RecommendedUsername,
    string Note,
    string Status,
    string ReviewedAt,
    string ReviewedBy
);

public sealed record SupportTicketVm(
    long Id,
    string Date,
    string Author,
    string Topic,
    string Details,
    string Status
);

public sealed record ReviewResultVm(bool Success, string Message);
