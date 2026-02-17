using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace WebPanel.Services;

public sealed class ExcelAdminDataService(IOptions<ExcelSyncOptions> options) : IAdminDataService
{
    private readonly ExcelSyncOptions _options = options.Value;
    private readonly object _sync = new();

    public DashboardVm GetDashboard()
    {
        var drone = ReadRequests("drone", "active").Count;
        var droneCompleted = ReadRequests("drone", "completed").Count;

        var repair = ReadRequests("repair", "active").Count;
        var repairCompleted = ReadRequests("repair", "completed").Count;

        var consumables = ReadRequests("consumables", "active").Count;
        var consumablesCompleted = ReadRequests("consumables", "completed").Count;

        var pending = GetPendingRecommendations().Count;
        return new DashboardVm(drone, droneCompleted, repair, repairCompleted, consumables, consumablesCompleted, pending);
    }

    public IReadOnlyList<RequestVm> GetRequests(string category, string status)
        => ReadRequests(category, status);

    public IReadOnlyList<UserAccessVm> GetUsers()
    {
        lock (_sync)
        {
            var path = Path.Combine(GetTablesDir(), _options.AccessFileName);
            if (!File.Exists(path))
            {
                return [];
            }

            using var wb = new XLWorkbook(path);
            if (!wb.TryGetWorksheet("Users", out var ws))
            {
                return [];
            }

            var result = new List<UserAccessVm>();
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= last; r++)
            {
                if (!long.TryParse(ws.Cell(r, 1).GetString(), out var userId))
                {
                    continue;
                }

                result.Add(new UserAccessVm(
                    userId,
                    ws.Cell(r, 9).GetString(),
                    ws.Cell(r, 2).GetString(),
                    ws.Cell(r, 3).GetString() == "1",
                    ws.Cell(r, 4).GetString() == "1",
                    ws.Cell(r, 5).GetString() == "1",
                    ws.Cell(r, 6).GetString() == "1",
                    ws.Cell(r, 7).GetString() == "1"));
            }

            return result.OrderBy(x => x.UserId).ToList();
        }
    }

    public IReadOnlyList<RecommendationVm> GetPendingRecommendations()
    {
        lock (_sync)
        {
            var path = Path.Combine(GetTablesDir(), _options.AccessFileName);
            if (!File.Exists(path))
            {
                return [];
            }

            using var wb = new XLWorkbook(path);
            if (!wb.TryGetWorksheet("Recommendations", out var ws))
            {
                return [];
            }

            var result = new List<RecommendationVm>();
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= last; r++)
            {
                if (!long.TryParse(ws.Cell(r, 1).GetString(), out var id))
                {
                    continue;
                }

                var status = string.IsNullOrWhiteSpace(ws.Cell(r, 7).GetString()) ? "pending" : ws.Cell(r, 7).GetString();
                if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new RecommendationVm(
                    id,
                    ws.Cell(r, 2).GetString(),
                    ws.Cell(r, 3).GetString(),
                    ws.Cell(r, 4).GetString(),
                    ws.Cell(r, 6).GetString(),
                    status,
                    ws.Cell(r, 8).GetString(),
                    ws.Cell(r, 9).GetString()));
            }

            return result.OrderByDescending(x => x.Id).ToList();
        }
    }


    public IReadOnlyList<SupportTicketVm> GetSupportTickets(int take = 50)
    {
        lock (_sync)
        {
            var path = Path.Combine(GetTablesDir(), _options.SupportFileName);
            if (!File.Exists(path))
            {
                return [];
            }

            using var wb = new XLWorkbook(path);
            if (!wb.TryGetWorksheet("Support", out var ws))
            {
                return [];
            }

            var result = new List<SupportTicketVm>();
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= last; r++)
            {
                if (!long.TryParse(ws.Cell(r, 1).GetString(), out var id))
                {
                    continue;
                }

                result.Add(new SupportTicketVm(
                    id,
                    ws.Cell(r, 2).GetString(),
                    ws.Cell(r, 3).GetString(),
                    ws.Cell(r, 4).GetString(),
                    ws.Cell(r, 5).GetString(),
                    string.IsNullOrWhiteSpace(ws.Cell(r, 6).GetString()) ? "open" : ws.Cell(r, 6).GetString()));
            }

            return result.OrderByDescending(x => x.Id).Take(take).ToList();
        }
    }

    public ReviewResultVm AddSupportTicket(string author, string topic, string details)
    {
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(details))
        {
            return new ReviewResultVm(false, "Тема и описание обращения обязательны.");
        }

        lock (_sync)
        {
            var path = Path.Combine(GetTablesDir(), _options.SupportFileName);
            EnsureDirectory(path);
            using var wb = File.Exists(path) ? new XLWorkbook(path) : new XLWorkbook();
            var ws = EnsureSupportSheet(wb);

            var nextId = NextSupportId(ws);
            var row = ws.LastRowUsed()?.RowNumber() + 1 ?? 2;
            ws.Cell(row, 1).Value = nextId;
            ws.Cell(row, 2).Value = DateTime.Now.ToString("s");
            ws.Cell(row, 3).Value = string.IsNullOrWhiteSpace(author) ? "webpanel" : author;
            ws.Cell(row, 4).Value = topic.Trim();
            ws.Cell(row, 5).Value = details.Trim();
            ws.Cell(row, 6).Value = "open";

            wb.SaveAs(path);
            return new ReviewResultVm(true, $"Обращение #{nextId} создано.");
        }
    }

    public ReviewResultVm ReviewRecommendation(long recommendationId, bool accept, string reviewer)
    {
        lock (_sync)
        {
            var path = Path.Combine(GetTablesDir(), _options.AccessFileName);
            if (!File.Exists(path))
            {
                return new ReviewResultVm(false, "Файл access_users.xlsx не найден.");
            }

            using var wb = new XLWorkbook(path);
            if (!wb.TryGetWorksheet("Recommendations", out var recWs))
            {
                return new ReviewResultVm(false, "Лист Recommendations не найден.");
            }

            var last = recWs.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= last; r++)
            {
                if (!long.TryParse(recWs.Cell(r, 1).GetString(), out var id) || id != recommendationId)
                {
                    continue;
                }

                var status = string.IsNullOrWhiteSpace(recWs.Cell(r, 7).GetString()) ? "pending" : recWs.Cell(r, 7).GetString();
                if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    return new ReviewResultVm(false, "Рекомендация уже обработана.");
                }

                recWs.Cell(r, 7).Value = accept ? "accepted" : "rejected";
                recWs.Cell(r, 8).Value = DateTime.Now.ToString("s");
                recWs.Cell(r, 9).Value = reviewer;

                if (accept)
                {
                    GrantAccessByUsername(wb, recWs.Cell(r, 4).GetString());
                }

                wb.SaveAs(path);
                return new ReviewResultVm(true, accept ? "Рекомендация принята." : "Рекомендация отклонена.");
            }

            return new ReviewResultVm(false, "Рекомендация не найдена.");
        }
    }

    private List<RequestVm> ReadRequests(string category, string status)
    {
        lock (_sync)
        {
            return category switch
            {
                "repair" => ReadRepairs(status),
                "consumables" => ReadConsumables(status),
                _ => ReadApplications(status)
            };
        }
    }

    private List<RequestVm> ReadApplications(string status)
    {
        var path = Path.Combine(GetTablesDir(), _options.ApplicationsFileName);
        if (!File.Exists(path)) return [];

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Applications");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        var result = new List<RequestVm>();

        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var id)) continue;
            if (NormalizeAppStatus(ws.Cell(r, 17).GetString()) != status) continue;

            result.Add(new RequestVm(
                id,
                "Заявка на дроны",
                ws.Cell(r, 3).GetString(),
                ws.Cell(r, 2).GetString(),
                ws.Cell(r, 6).GetString(),
                $"Позывной: {ws.Cell(r, 5).GetString()}; Тип дрона: {ws.Cell(r, 8).GetString()}; Кол-во: {ws.Cell(r, 15).GetString()}",
                ws.Cell(r, 16).GetString(),
                status));
        }

        return result.OrderByDescending(x => x.Id).ToList();
    }

    private List<RequestVm> ReadRepairs(string status)
    {
        var path = Path.Combine(GetTablesDir(), _options.RepairsFileName);
        if (!File.Exists(path)) return [];

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Repairs");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        var result = new List<RequestVm>();

        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var id)) continue;
            if (NormalizeWorkStatus(ws.Cell(r, 9).GetString()) != status) continue;

            result.Add(new RequestVm(
                id,
                "Ремонт",
                ws.Cell(r, 3).GetString(),
                ws.Cell(r, 2).GetString(),
                ws.Cell(r, 4).GetString(),
                $"Оборудование: {ws.Cell(r, 5).GetString()}; Неисправность: {ws.Cell(r, 6).GetString()}; Кол-во: {ws.Cell(r, 7).GetString()}",
                ws.Cell(r, 8).GetString(),
                status));
        }

        return result.OrderByDescending(x => x.Id).ToList();
    }

    private List<RequestVm> ReadConsumables(string status)
    {
        var path = Path.Combine(GetTablesDir(), _options.ConsumablesFileName);
        if (!File.Exists(path)) return [];

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Consumables");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        var result = new List<RequestVm>();

        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var id)) continue;
            if (NormalizeWorkStatus(ws.Cell(r, 8).GetString()) != status) continue;

            result.Add(new RequestVm(
                id,
                "Комплектующие",
                ws.Cell(r, 2).GetString(),
                ws.Cell(r, 3).GetString(),
                ws.Cell(r, 4).GetString(),
                $"Необходимо: {ws.Cell(r, 5).GetString()}; Кол-во: {ws.Cell(r, 6).GetString()}",
                ws.Cell(r, 7).GetString(),
                status));
        }

        return result.OrderByDescending(x => x.Id).ToList();
    }

    private static string NormalizeAppStatus(string status)
        => string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "active";

    private static string NormalizeWorkStatus(string status)
        => string.Equals(status, "Завершено", StringComparison.OrdinalIgnoreCase) ? "completed" : "active";

    private string GetTablesDir() => Path.GetFullPath(_options.TablesDirectory);

    private static void GrantAccessByUsername(IXLWorkbook wb, string usernameRaw)
    {
        var username = NormalizeUsername(usernameRaw);
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        if (!wb.TryGetWorksheet("Users", out var usersWs))
        {
            usersWs = wb.Worksheets.Add("Users");
        }

        var last = usersWs.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var storedUsername = NormalizeUsername(usersWs.Cell(r, 9).GetString());
            if (!string.Equals(storedUsername, username, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            usersWs.Cell(r, 3).Value = "1";
            return;
        }

        if (!wb.TryGetWorksheet("UsernameAccess", out var usernameWs))
        {
            usernameWs = wb.Worksheets.Add("UsernameAccess");
            usernameWs.Cell(1, 1).Value = "Username";
            usernameWs.Cell(1, 2).Value = "CanUseBot";
            usernameWs.Cell(1, 3).Value = "AddedAt";
        }

        var usernameLast = usernameWs.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= usernameLast; r++)
        {
            if (!string.Equals(NormalizeUsername(usernameWs.Cell(r, 1).GetString()), username, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            usernameWs.Cell(r, 2).Value = "1";
            return;
        }

        var row = usernameLast + 1;
        usernameWs.Cell(row, 1).Value = username;
        usernameWs.Cell(row, 2).Value = "1";
        usernameWs.Cell(row, 3).Value = DateTime.Now.ToString("s");
    }


    private static IXLWorksheet EnsureSupportSheet(XLWorkbook wb)
    {
        if (!wb.TryGetWorksheet("Support", out var ws))
        {
            ws = wb.Worksheets.Add("Support");
        }

        ws.Cell(1, 1).Value = "ID";
        ws.Cell(1, 2).Value = "Дата";
        ws.Cell(1, 3).Value = "Автор";
        ws.Cell(1, 4).Value = "Тема";
        ws.Cell(1, 5).Value = "Описание";
        ws.Cell(1, 6).Value = "Статус";
        ws.Range(1, 1, 1, 6).Style.Font.Bold = true;
        return ws;
    }

    private static long NextSupportId(IXLWorksheet ws)
    {
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        long max = 0;
        for (var r = 2; r <= last; r++)
        {
            if (long.TryParse(ws.Cell(r, 1).GetString(), out var id) && id > max)
            {
                max = id;
            }
        }

        return max + 1;
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return string.Empty;
        }

        return username.Trim().TrimStart('@').ToLowerInvariant();
    }
}
