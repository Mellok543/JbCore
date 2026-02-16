using Microsoft.EntityFrameworkCore;
using WebPanel.Data;

namespace WebPanel.Services;

public sealed class DbAdminDataService(AdminDbContext db) : IAdminDataService
{
    public DashboardVm GetDashboard()
    {
        var droneActive = db.Requests.Count(x => x.Category == "drone" && x.Status == "active");
        var droneCompleted = db.Requests.Count(x => x.Category == "drone" && x.Status == "completed");

        var repairActive = db.Requests.Count(x => x.Category == "repair" && x.Status == "active");
        var repairCompleted = db.Requests.Count(x => x.Category == "repair" && x.Status == "completed");

        var consumablesActive = db.Requests.Count(x => x.Category == "consumables" && x.Status == "active");
        var consumablesCompleted = db.Requests.Count(x => x.Category == "consumables" && x.Status == "completed");

        var pendingRecommendations = db.Recommendations.Count(x => x.Status == "pending");

        return new DashboardVm(
            droneActive,
            droneCompleted,
            repairActive,
            repairCompleted,
            consumablesActive,
            consumablesCompleted,
            pendingRecommendations
        );
    }

    public IReadOnlyList<RequestVm> GetRequests(string category, string status)
    {
        return db.Requests
            .AsNoTracking()
            .Where(x => x.Category == category && x.Status == status)
            .OrderByDescending(x => x.Id)
            .Select(x => new RequestVm(
                x.Id,
                ToCategoryTitle(x.Category),
                x.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                x.Reporter,
                x.Unit,
                x.Description,
                x.Note,
                x.Status))
            .ToList();
    }

    public IReadOnlyList<UserAccessVm> GetUsers()
    {
        return db.Users
            .AsNoTracking()
            .OrderBy(x => x.UserId)
            .Select(x => new UserAccessVm(
                x.UserId,
                x.Username,
                x.DisplayName,
                x.CanUseBot,
                x.CanComplete,
                x.CanManageAccess,
                x.NotifyRequests,
                x.NotifyRecommendations))
            .ToList();
    }

    public IReadOnlyList<RecommendationVm> GetPendingRecommendations()
    {
        return db.Recommendations
            .AsNoTracking()
            .Where(x => x.Status == "pending")
            .OrderByDescending(x => x.Id)
            .Select(x => new RecommendationVm(
                x.Id,
                x.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                x.Recommender,
                x.RecommendedUsername,
                x.Note,
                x.Status,
                x.ReviewedAtUtc.HasValue ? x.ReviewedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                x.ReviewedBy))
            .ToList();
    }

    public ReviewResultVm ReviewRecommendation(long recommendationId, bool accept, string reviewer)
    {
        var recommendation = db.Recommendations.SingleOrDefault(x => x.Id == recommendationId);
        if (recommendation is null)
        {
            return new ReviewResultVm(false, "Рекомендация не найдена.");
        }

        if (!string.Equals(recommendation.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return new ReviewResultVm(false, "Рекомендация уже была обработана ранее.");
        }

        recommendation.Status = accept ? "accepted" : "rejected";
        recommendation.ReviewedAtUtc = DateTime.UtcNow;
        recommendation.ReviewedBy = reviewer;

        if (accept)
        {
            var normalized = NormalizeUsername(recommendation.RecommendedUsername);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                var user = db.Users.SingleOrDefault(x => x.Username == normalized);
                if (user is not null)
                {
                    user.CanUseBot = true;
                }
            }
        }

        db.SaveChanges();

        return new ReviewResultVm(true, accept ? "Рекомендация принята." : "Рекомендация отклонена.");
    }

    private static string NormalizeUsername(string username)
        => username.Trim().TrimStart('@');

    private static string ToCategoryTitle(string category)
    {
        return category switch
        {
            "repair" => "Ремонт",
            "consumables" => "Комплектующие",
            _ => "Заявка на дроны"
        };
    }
}
