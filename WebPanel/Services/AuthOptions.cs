namespace WebPanel.Services;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public List<AuthUser> Users { get; set; } = [];
}

public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "operator";
}
