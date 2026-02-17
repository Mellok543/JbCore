namespace WebPanel.Services;

public sealed class TelegramNotificationsOptions
{
    public const string SectionName = "TelegramNotifications";
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatIds { get; set; } = string.Empty;
}

public interface ITelegramNotifier
{
    Task NotifySupportTicketAsync(long ticketId, string author, string topic, string details, CancellationToken cancellationToken = default);
}
