using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace WebPanel.Services;

public sealed class TelegramNotifier(HttpClient httpClient, IOptions<TelegramNotificationsOptions> options) : ITelegramNotifier
{
    private readonly TelegramNotificationsOptions _options = options.Value;

    public async Task NotifySupportTicketAsync(long ticketId, string author, string topic, string details, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.ChatIds))
        {
            return;
        }

        var chats = _options.ChatIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (chats.Count == 0)
        {
            return;
        }

        var text = $"🆘 Новое обращение в WebPanel\n" +
                   $"ID: #{ticketId}\n" +
                   $"Автор: {author}\n" +
                   $"Тема: {topic}\n" +
                   $"Описание: {details}";

        foreach (var chatId in chats)
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            var payload = new { chat_id = chatId, text };
            try
            {
                await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            }
            catch
            {
                // intentionally swallow to avoid breaking support flow
            }
        }
    }
}
