namespace WebPanel.Services;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string UsersFileName { get; set; } = "site_users.xlsx";
    public string BootstrapAdminUsername { get; set; } = "admin";
    public string BootstrapAdminPassword { get; set; } = "change_me";
    public string BootstrapAdminDisplayName { get; set; } = "Administrator";
}

public sealed record SiteUserVm(
    string Username,
    string DisplayName,
    string Role,
    string CreatedAt
);

public sealed record AuthResult(bool Success, string Message, SiteUserVm? User = null);

public interface IAuthService
{
    AuthResult ValidateCredentials(string username, string password);
    IReadOnlyList<SiteUserVm> GetUsers();
    AuthResult CreateUser(string username, string password, string displayName, string role);
}
