using ClosedXML.Excel;

namespace WebPanel.Services;

public sealed class ExcelAdminService : IAdminDataService
{
    private readonly string _applicationsPath;
    private readonly string _repairsPath;
    private readonly string _consumablesPath;
    private readonly string _accessPath;

    public ExcelAdminService(IConfiguration configuration)
    {
        var tablesDirectory = configuration["TablesDirectory"];
        if (string.IsNullOrWhiteSpace(tablesDirectory))
        {
            tablesDirectory = AppContext.BaseDirectory;
        }

        Directory.CreateDirectory(tablesDirectory);

        _applicationsPath = Path.Combine(tablesDirectory, configuration["ApplicationsFileName"] ?? "applications.xlsx");
        _repairsPath = Path.Combine(tablesDirectory, configuration["RepairsFileName"] ?? "repairs.xlsx");
        _consumablesPath = Path.Combine(tablesDirectory, configuration["ConsumablesFileName"] ?? "consumables.xlsx");
        _accessPath = Path.Combine(tablesDirectory, configuration["AccessFileName"] ?? "access_users.xlsx");
    }

    public DashboardVm GetDashboard()
    {
        var droneActive = GetDroneRequests("active").Count;
        var droneCompleted = GetDroneRequests("completed").Count;

        var repairActive = GetRepairRequests("В работе").Count;
        var repairCompleted = GetRepairRequests("Завершено").Count;

        var consumablesActive = GetConsumablesRequests("В работе").Count;
        var consumablesCompleted = GetConsumablesRequests("Завершено").Count;

        var pendingRecommendations = GetPendingRecommendations().Count;

        return new DashboardVm(
            droneActive,
            droneCompleted,
            repairActive,
            repairCompleted,
            consumablesActive,
            consumablesCompleted,
            pendingRecommendations);
    }

    public IReadOnlyList<RequestVm> GetRequests(string category, string status)
    {
        return category switch
        {
            "repair" => GetRepairRequests(status == "completed" ? "Завершено" : "В работе"),
            "consumables" => GetConsumablesRequests(status == "completed" ? "Завершено" : "В работе"),
            _ => GetDroneRequests(status == "completed" ? "completed" : "active")
        };
    }

    public IReadOnlyList<UserAccessVm> GetUsers()
    {
        if (!File.Exists(_accessPath)) return [];

        using var workbook = new XLWorkbook(_accessPath);
        if (!workbook.TryGetWorksheet("Users", out var ws)) return [];

        var list = new List<UserAccessVm>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var userIdRaw = ws.Cell(row, 1).GetString();
            if (!long.TryParse(userIdRaw, out var userId)) continue;

            list.Add(new UserAccessVm(
                userId,
                ws.Cell(row, 9).GetString(),
                ws.Cell(row, 2).GetString(),
                ws.Cell(row, 3).GetString() == "1",
                ws.Cell(row, 4).GetString() == "1",
                ws.Cell(row, 5).GetString() == "1",
                ws.Cell(row, 6).GetString() == "1",
                ws.Cell(row, 7).GetString() == "1"
            ));
        }

        return list.OrderBy(x => x.UserId).ToList();
    }

    public IReadOnlyList<RecommendationVm> GetPendingRecommendations()
    {
        if (!File.Exists(_accessPath)) return [];

        using var workbook = new XLWorkbook(_accessPath);
        if (!workbook.TryGetWorksheet("Recommendations", out var ws)) return [];

        var list = new List<RecommendationVm>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!long.TryParse(ws.Cell(row, 1).GetString(), out var id)) continue;
            var status = ws.Cell(row, 7).GetString();
            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(new RecommendationVm(
                id,
                ws.Cell(row, 2).GetString(),
                ws.Cell(row, 3).GetString(),
                ws.Cell(row, 4).GetString(),
                ws.Cell(row, 6).GetString(),
                status,
                ws.Cell(row, 8).GetString(),
                ws.Cell(row, 9).GetString()
            ));
        }

        return list.OrderByDescending(x => x.Id).ToList();
    }

    public ReviewResultVm ReviewRecommendation(long recommendationId, bool accept, string reviewer)
    {
        if (!File.Exists(_accessPath))
        {
            return new ReviewResultVm(false, "Файл access_users.xlsx не найден.");
        }

        using var workbook = new XLWorkbook(_accessPath);
        if (!workbook.TryGetWorksheet("Recommendations", out var recommendations))
        {
            return new ReviewResultVm(false, "Лист Recommendations не найден.");
        }

        var lastRow = recommendations.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!long.TryParse(recommendations.Cell(row, 1).GetString(), out var id) || id != recommendationId)
            {
                continue;
            }

            var status = recommendations.Cell(row, 7).GetString();
            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return new ReviewResultVm(false, "Рекомендация уже была обработана ранее.");
            }

            recommendations.Cell(row, 7).Value = accept ? "accepted" : "rejected";
            recommendations.Cell(row, 8).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            recommendations.Cell(row, 9).Value = reviewer;

            if (accept)
            {
                var username = NormalizeUsername(recommendations.Cell(row, 4).GetString());
                if (!string.IsNullOrWhiteSpace(username))
                {
                    GrantUsernameAccess(workbook, username);
                }
            }

            workbook.Save();
            return new ReviewResultVm(true, accept ? "Рекомендация принята." : "Рекомендация отклонена.");
        }

        return new ReviewResultVm(false, "Рекомендация не найдена.");
    }

    private void GrantUsernameAccess(XLWorkbook workbook, string username)
    {
        if (workbook.TryGetWorksheet("Users", out var usersWs))
        {
            var usersLastRow = usersWs.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= usersLastRow; row++)
            {
                var currentUsername = NormalizeUsername(usersWs.Cell(row, 9).GetString());
                if (!string.Equals(currentUsername, username, StringComparison.OrdinalIgnoreCase)) continue;
                usersWs.Cell(row, 3).Value = "1";
                return;
            }
        }

        if (!workbook.TryGetWorksheet("UsernameAccess", out var usernameWs))
        {
            usernameWs = workbook.AddWorksheet("UsernameAccess");
            usernameWs.Cell(1, 1).Value = "Username";
            usernameWs.Cell(1, 2).Value = "CanUseBot";
            usernameWs.Cell(1, 3).Value = "AddedAt";
            usernameWs.Range(1, 1, 1, 3).Style.Font.Bold = true;
        }

        var usernameLastRow = usernameWs.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= usernameLastRow; row++)
        {
            var currentUsername = NormalizeUsername(usernameWs.Cell(row, 1).GetString());
            if (!string.Equals(currentUsername, username, StringComparison.OrdinalIgnoreCase)) continue;
            usernameWs.Cell(row, 2).Value = "1";
            return;
        }

        var insertRow = usernameLastRow + 1;
        usernameWs.Cell(insertRow, 1).Value = username;
        usernameWs.Cell(insertRow, 2).Value = "1";
        usernameWs.Cell(insertRow, 3).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return string.Empty;
        return username.Trim().TrimStart('@');
    }

    private List<RequestVm> GetDroneRequests(string status)
    {
        if (!File.Exists(_applicationsPath)) return [];

        using var workbook = new XLWorkbook(_applicationsPath);
        if (!workbook.TryGetWorksheet("Applications", out var ws)) return [];

        var list = new List<RequestVm>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!string.Equals(ws.Cell(row, 7).GetString(), status, StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(new RequestVm(
                ParseId(ws.Cell(row, 1).GetString()),
                "Заявка на дроны",
                ws.Cell(row, 2).GetString(),
                ws.Cell(row, 3).GetString(),
                ws.Cell(row, 4).GetString(),
                ws.Cell(row, 5).GetString(),
                ws.Cell(row, 6).GetString(),
                ws.Cell(row, 7).GetString()
            ));
        }

        return list.OrderByDescending(x => x.Id).ToList();
    }

    private List<RequestVm> GetRepairRequests(string status)
    {
        if (!File.Exists(_repairsPath)) return [];

        using var workbook = new XLWorkbook(_repairsPath);
        if (!workbook.TryGetWorksheet("Repairs", out var ws)) return [];

        var list = new List<RequestVm>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!string.Equals(ws.Cell(row, 9).GetString(), status, StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(new RequestVm(
                ParseId(ws.Cell(row, 1).GetString()),
                "Ремонт",
                ws.Cell(row, 2).GetString(),
                ws.Cell(row, 3).GetString(),
                ws.Cell(row, 4).GetString(),
                ws.Cell(row, 6).GetString(),
                ws.Cell(row, 7).GetString(),
                ws.Cell(row, 9).GetString()
            ));
        }

        return list.OrderByDescending(x => x.Id).ToList();
    }

    private List<RequestVm> GetConsumablesRequests(string status)
    {
        if (!File.Exists(_consumablesPath)) return [];

        using var workbook = new XLWorkbook(_consumablesPath);
        if (!workbook.TryGetWorksheet("Consumables", out var ws)) return [];

        var list = new List<RequestVm>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!string.Equals(ws.Cell(row, 8).GetString(), status, StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(new RequestVm(
                ParseId(ws.Cell(row, 1).GetString()),
                "Комплектующие",
                ws.Cell(row, 2).GetString(),
                ws.Cell(row, 3).GetString(),
                ws.Cell(row, 4).GetString(),
                ws.Cell(row, 5).GetString(),
                ws.Cell(row, 7).GetString(),
                ws.Cell(row, 8).GetString()
            ));
        }

        return list.OrderByDescending(x => x.Id).ToList();
    }

    private static long ParseId(string raw)
    {
        return long.TryParse(raw, out var id) ? id : 0;
    }
}
