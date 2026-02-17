using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace WebPanel.Services;

public sealed class ExcelAuthService(IOptions<AuthOptions> authOptions, IOptions<ExcelSyncOptions> excelOptions) : IAuthService
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly ExcelSyncOptions _excel = excelOptions.Value;
    private readonly object _sync = new();

    public AuthResult ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Введите логин и пароль.");
        }

        lock (_sync)
        {
            EnsureBootstrapAdmin();
            var path = GetUsersPath();
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Users");
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= last; r++)
            {
                var storedUsername = ws.Cell(r, 1).GetString();
                if (!string.Equals(storedUsername, username.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var hash = ws.Cell(r, 2).GetString();
                if (!string.Equals(hash, HashPassword(password), StringComparison.Ordinal))
                {
                    return new AuthResult(false, "Неверный логин или пароль.");
                }

                var user = new SiteUserVm(
                    storedUsername,
                    ws.Cell(r, 3).GetString(),
                    string.IsNullOrWhiteSpace(ws.Cell(r, 4).GetString()) ? "operator" : ws.Cell(r, 4).GetString(),
                    ws.Cell(r, 5).GetString());

                return new AuthResult(true, "ok", user);
            }

            return new AuthResult(false, "Неверный логин или пароль.");
        }
    }

    public IReadOnlyList<SiteUserVm> GetUsers()
    {
        lock (_sync)
        {
            EnsureBootstrapAdmin();
            var path = GetUsersPath();
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Users");
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;
            var users = new List<SiteUserVm>();

            for (var r = 2; r <= last; r++)
            {
                var username = ws.Cell(r, 1).GetString();
                if (string.IsNullOrWhiteSpace(username))
                {
                    continue;
                }

                users.Add(new SiteUserVm(
                    username,
                    ws.Cell(r, 3).GetString(),
                    string.IsNullOrWhiteSpace(ws.Cell(r, 4).GetString()) ? "operator" : ws.Cell(r, 4).GetString(),
                    ws.Cell(r, 5).GetString()));
            }

            return users.OrderBy(x => x.Username).ToList();
        }
    }

    public AuthResult CreateUser(string username, string password, string displayName, string role)
    {
        var cleanUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(cleanUsername) || cleanUsername.Length < 3)
        {
            return new AuthResult(false, "Логин должен быть не менее 3 символов.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return new AuthResult(false, "Пароль должен быть не менее 6 символов.");
        }

        var cleanRole = role == "admin" ? "admin" : "operator";

        lock (_sync)
        {
            EnsureBootstrapAdmin();
            var path = GetUsersPath();
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Users");
            var last = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= last; r++)
            {
                if (string.Equals(ws.Cell(r, 1).GetString(), cleanUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return new AuthResult(false, "Пользователь с таким логином уже существует.");
                }
            }

            var row = last + 1;
            ws.Cell(row, 1).Value = cleanUsername;
            ws.Cell(row, 2).Value = HashPassword(password);
            ws.Cell(row, 3).Value = string.IsNullOrWhiteSpace(displayName) ? cleanUsername : displayName.Trim();
            ws.Cell(row, 4).Value = cleanRole;
            ws.Cell(row, 5).Value = DateTime.Now.ToString("s");
            wb.SaveAs(path);

            return new AuthResult(true, $"Пользователь {cleanUsername} создан.");
        }
    }

    private void EnsureBootstrapAdmin()
    {
        var path = GetUsersPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var wb = File.Exists(path) ? new XLWorkbook(path) : new XLWorkbook();
        var ws = wb.TryGetWorksheet("Users", out var usersWs) ? usersWs : wb.Worksheets.Add("Users");

        ws.Cell(1, 1).Value = "Username";
        ws.Cell(1, 2).Value = "PasswordHash";
        ws.Cell(1, 3).Value = "DisplayName";
        ws.Cell(1, 4).Value = "Role";
        ws.Cell(1, 5).Value = "CreatedAt";

        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            if (string.Equals(ws.Cell(r, 1).GetString(), _auth.BootstrapAdminUsername, StringComparison.OrdinalIgnoreCase))
            {
                wb.SaveAs(path);
                return;
            }
        }

        var row = last + 1;
        ws.Cell(row, 1).Value = _auth.BootstrapAdminUsername;
        ws.Cell(row, 2).Value = HashPassword(_auth.BootstrapAdminPassword);
        ws.Cell(row, 3).Value = _auth.BootstrapAdminDisplayName;
        ws.Cell(row, 4).Value = "admin";
        ws.Cell(row, 5).Value = DateTime.Now.ToString("s");
        wb.SaveAs(path);
    }

    private string GetUsersPath() => Path.Combine(Path.GetFullPath(_excel.TablesDirectory), _auth.UsersFileName);

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
