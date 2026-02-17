using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPanel.Data;

namespace WebPanel.Services;

public sealed class ExcelToPostgresSyncService(AdminDbContext db, IOptions<ExcelSyncOptions> options)
{
    private readonly ExcelSyncOptions _options = options.Value;

    public SyncReport Sync()
    {
        var tablesDir = Path.GetFullPath(_options.TablesDirectory);

        var report = new SyncReport();
        report.Details.Add(SyncApplications(Path.Combine(tablesDir, _options.ApplicationsFileName), report));
        report.Details.Add(SyncRepairs(Path.Combine(tablesDir, _options.RepairsFileName), report));
        report.Details.Add(SyncConsumables(Path.Combine(tablesDir, _options.ConsumablesFileName), report));
        report.Details.Add(SyncAccess(Path.Combine(tablesDir, _options.AccessFileName), report));

        db.SaveChanges();
        return report;
    }

    private SyncDetail SyncApplications(string path, SyncReport report)
    {
        const string category = "drone";
        if (!File.Exists(path)) return SyncDetail.Skipped(path, "file not found");

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Applications");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;

        var detail = new SyncDetail(path, category);
        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var sourceId))
            {
                continue;
            }

            var status = NormalizeStatus(ws.Cell(r, 17).GetString(), category);
            var entity = UpsertRequest(category, sourceId);
            entity.ExternalId = sourceId;
            entity.CreatedAtUtc = ParseUtc(ws.Cell(r, 3).GetString());
            entity.Reporter = EmptyFallback(ws.Cell(r, 2).GetString(), "-");
            entity.Unit = EmptyFallback(ws.Cell(r, 6).GetString(), "-");
            entity.Description = $"Позывной: {EmptyFallback(ws.Cell(r, 5).GetString(), "-")}; Тип дрона: {EmptyFallback(ws.Cell(r, 8).GetString(), "-")}; Кол-во: {EmptyFallback(ws.Cell(r, 15).GetString(), "-")}";
            entity.Note = EmptyFallback(ws.Cell(r, 16).GetString(), "-");
            entity.Status = status;

            detail.ImportedRows++;
            if (status == "active") report.ActiveImported++; else report.CompletedImported++;
        }

        return detail;
    }

    private SyncDetail SyncRepairs(string path, SyncReport report)
    {
        const string category = "repair";
        if (!File.Exists(path)) return SyncDetail.Skipped(path, "file not found");

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Repairs");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;

        var detail = new SyncDetail(path, category);
        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var sourceId))
            {
                continue;
            }

            var status = NormalizeStatus(ws.Cell(r, 9).GetString(), category);
            var entity = UpsertRequest(category, sourceId);
            entity.ExternalId = sourceId;
            entity.CreatedAtUtc = ParseUtc(ws.Cell(r, 3).GetString());
            entity.Reporter = EmptyFallback(ws.Cell(r, 2).GetString(), "-");
            entity.Unit = EmptyFallback(ws.Cell(r, 4).GetString(), "-");
            entity.Description = $"Оборудование: {EmptyFallback(ws.Cell(r, 5).GetString(), "-")}; Неисправность: {EmptyFallback(ws.Cell(r, 6).GetString(), "-")}; Кол-во: {EmptyFallback(ws.Cell(r, 7).GetString(), "-")}";
            entity.Note = EmptyFallback(ws.Cell(r, 8).GetString(), "-");
            entity.Status = status;

            detail.ImportedRows++;
            if (status == "active") report.ActiveImported++; else report.CompletedImported++;
        }

        return detail;
    }

    private SyncDetail SyncConsumables(string path, SyncReport report)
    {
        const string category = "consumables";
        if (!File.Exists(path)) return SyncDetail.Skipped(path, "file not found");

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Consumables");
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;

        var detail = new SyncDetail(path, category);
        for (var r = 2; r <= last; r++)
        {
            if (!long.TryParse(ws.Cell(r, 1).GetString(), out var sourceId))
            {
                continue;
            }

            var status = NormalizeStatus(ws.Cell(r, 8).GetString(), category);
            var entity = UpsertRequest(category, sourceId);
            entity.ExternalId = sourceId;
            entity.CreatedAtUtc = ParseUtc(ws.Cell(r, 2).GetString());
            entity.Reporter = EmptyFallback(ws.Cell(r, 3).GetString(), "-");
            entity.Unit = EmptyFallback(ws.Cell(r, 4).GetString(), "-");
            entity.Description = $"Необходимо: {EmptyFallback(ws.Cell(r, 5).GetString(), "-")}; Кол-во: {EmptyFallback(ws.Cell(r, 6).GetString(), "-")}";
            entity.Note = EmptyFallback(ws.Cell(r, 7).GetString(), "-");
            entity.Status = status;

            detail.ImportedRows++;
            if (status == "active") report.ActiveImported++; else report.CompletedImported++;
        }

        return detail;
    }

    private SyncDetail SyncAccess(string path, SyncReport report)
    {
        if (!File.Exists(path)) return SyncDetail.Skipped(path, "file not found");

        using var wb = new XLWorkbook(path);

        var usersWs = wb.Worksheet("Users");
        var usersLast = usersWs.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= usersLast; r++)
        {
            if (!long.TryParse(usersWs.Cell(r, 1).GetString(), out var userId))
            {
                continue;
            }

            var user = db.Users.SingleOrDefault(x => x.UserId == userId) ?? new UserAccessEntity { UserId = userId };
            user.DisplayName = EmptyFallback(usersWs.Cell(r, 2).GetString(), "-");
            user.CanUseBot = usersWs.Cell(r, 3).GetString() == "1";
            user.CanComplete = usersWs.Cell(r, 4).GetString() == "1";
            user.CanManageAccess = usersWs.Cell(r, 5).GetString() == "1";
            user.NotifyRequests = usersWs.Cell(r, 6).GetString() == "1";
            user.NotifyRecommendations = usersWs.Cell(r, 7).GetString() == "1";
            user.Username = NormalizeUsername(usersWs.Cell(r, 9).GetString());

            if (db.Entry(user).State == EntityState.Detached)
            {
                db.Users.Add(user);
            }

            report.UsersImported++;
        }

        if (!wb.TryGetWorksheet("Recommendations", out var recWs))
        {
            return new SyncDetail(path, "access") { ImportedRows = report.UsersImported };
        }

        var recLast = recWs.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= recLast; r++)
        {
            if (!long.TryParse(recWs.Cell(r, 1).GetString(), out var recId))
            {
                continue;
            }

            var rec = db.Recommendations.SingleOrDefault(x => x.Id == recId) ?? new RecommendationEntity { Id = recId };
            rec.CreatedAtUtc = ParseUtc(recWs.Cell(r, 2).GetString());
            rec.Recommender = EmptyFallback(recWs.Cell(r, 3).GetString(), "-");
            rec.RecommendedUsername = NormalizeUsername(recWs.Cell(r, 4).GetString());
            rec.Note = EmptyFallback(recWs.Cell(r, 6).GetString(), "-");
            rec.Status = NormalizeRecommendationStatus(recWs.Cell(r, 7).GetString());
            rec.ReviewedAtUtc = ParseOptionalUtc(recWs.Cell(r, 8).GetString());
            rec.ReviewedBy = EmptyFallback(recWs.Cell(r, 9).GetString(), string.Empty);

            if (db.Entry(rec).State == EntityState.Detached)
            {
                db.Recommendations.Add(rec);
            }

            report.RecommendationsImported++;
        }

        return new SyncDetail(path, "access") { ImportedRows = report.UsersImported + report.RecommendationsImported };
    }

    private RequestEntity UpsertRequest(string category, long sourceId)
    {
        var internalId = BuildInternalRequestId(category, sourceId);
        var entity = db.Requests.SingleOrDefault(x => x.Id == internalId);
        if (entity is not null)
        {
            return entity;
        }

        entity = new RequestEntity
        {
            Id = internalId,
            Category = category
        };

        db.Requests.Add(entity);
        return entity;
    }

    private static long BuildInternalRequestId(string category, long sourceId)
    {
        return category switch
        {
            "repair" => 1_000_000_000 + sourceId,
            "consumables" => 2_000_000_000 + sourceId,
            _ => sourceId
        };
    }

    private static string NormalizeStatus(string rawStatus, string category)
    {
        if (string.Equals(rawStatus, "completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawStatus, "Завершена", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawStatus, "Завершено", StringComparison.OrdinalIgnoreCase))
        {
            return "completed";
        }

        if (category is "repair" or "consumables" && string.Equals(rawStatus, "В работе", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        return "active";
    }

    private static string NormalizeRecommendationStatus(string rawStatus)
    {
        if (string.Equals(rawStatus, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            return "accepted";
        }

        if (string.Equals(rawStatus, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "rejected";
        }

        return "pending";
    }

    private static string EmptyFallback(string text, string fallback)
        => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private static string NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return string.Empty;
        }

        return username.Trim().TrimStart('@').ToLowerInvariant();
    }

    private static DateTime ParseUtc(string text)
    {
        if (!DateTime.TryParse(text, out var dt))
        {
            return DateTime.UtcNow;
        }

        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime()
        };
    }

    private static DateTime? ParseOptionalUtc(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return ParseUtc(text);
    }
}

public sealed class SyncReport
{
    public int ActiveImported { get; set; }
    public int CompletedImported { get; set; }
    public int UsersImported { get; set; }
    public int RecommendationsImported { get; set; }
    public List<SyncDetail> Details { get; } = [];
}

public sealed class SyncDetail(string sourcePath, string entity)
{
    public string SourcePath { get; set; } = sourcePath;
    public string Entity { get; set; } = entity;
    public int ImportedRows { get; set; }
    public string? SkipReason { get; set; }

    public static SyncDetail Skipped(string sourcePath, string reason)
        => new(sourcePath, "n/a") { SkipReason = reason };
}
