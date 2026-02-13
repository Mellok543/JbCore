using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN")?.Trim();
if (string.IsNullOrWhiteSpace(botToken))
{
    throw new InvalidOperationException("Укажите BOT_TOKEN в переменных окружения.");
}

var dbPath = Environment.GetEnvironmentVariable("DB_PATH")?.Trim();
dbPath = string.IsNullOrWhiteSpace(dbPath) ? "applications.db" : dbPath;

var closerIds = ParseCloserIds(Environment.GetEnvironmentVariable("CLOSER_IDS") ?? string.Empty);

var app = new BotApp(botToken!, dbPath!, closerIds);
await app.RunAsync();

static HashSet<long> ParseCloserIds(string raw)
{
    var result = new HashSet<long>();
    foreach (var piece in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (long.TryParse(piece, out var id))
        {
            result.Add(id);
        }
    }

    return result;
}

sealed class BotApp
{
    private const string ApiTemplate = "https://api.telegram.org/bot{0}/{1}";

    private readonly string _token;
    private readonly HttpClient _httpClient = new();
    private readonly ApplicationStore _store;
    private readonly HashSet<long> _closerIds;
    private readonly Dictionary<long, SessionState> _sessions = new();
    private int _offset;

    public BotApp(string token, string dbPath, HashSet<long> closerIds)
    {
        _token = token;
        _closerIds = closerIds;
        _store = new ApplicationStore(dbPath);
    }

    public async Task RunAsync()
    {
        Console.WriteLine("Бот запущен...");

        while (true)
        {
            try
            {
                var updates = await GetUpdatesAsync();
                foreach (var update in updates)
                {
                    _offset = update.UpdateId + 1;

                    var message = update.Message;
                    if (message is null || string.IsNullOrWhiteSpace(message.Text) || message.From is null)
                    {
                        continue;
                    }

                    var text = message.Text.Trim();
                    var chatId = message.Chat.Id;
                    var userId = message.From.Id;

                    if (text is "/start" or "Меню")
                    {
                        _sessions.Remove(userId);
                        await SendMessageAsync(chatId, "Выберите действие:", Keyboards.MainMenu);
                        continue;
                    }

                    if (text == "Оставить заявку")
                    {
                        _sessions[userId] = new SessionState("pilot_type");
                        await SendMessageAsync(chatId, "Какой ты тип?", Keyboards.PilotType);
                        continue;
                    }

                    if (text == "Активные заявки")
                    {
                        var active = _store.GetApplications("active");
                        if (active.Count == 0)
                        {
                            await SendMessageAsync(chatId, "Активных заявок пока нет.", Keyboards.MainMenu);
                        }
                        else
                        {
                            await SendMessageAsync(chatId, $"Активные заявки: {active.Count}", Keyboards.MainMenu);
                            foreach (var item in active)
                            {
                                await SendMessageAsync(chatId, item.FormatCard(), Keyboards.MainMenu);
                            }
                        }

                        continue;
                    }

                    if (text == "Завершенные заявки")
                    {
                        var completed = _store.GetApplications("completed");
                        if (completed.Count == 0)
                        {
                            await SendMessageAsync(chatId, "Завершённых заявок пока нет.", Keyboards.MainMenu);
                        }
                        else
                        {
                            await SendMessageAsync(chatId, $"Завершённые заявки: {completed.Count}", Keyboards.MainMenu);
                            foreach (var item in completed)
                            {
                                await SendMessageAsync(chatId, item.FormatCard(), Keyboards.MainMenu);
                            }
                        }

                        continue;
                    }

                    if (text.StartsWith("/complete", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_closerIds.Contains(userId))
                        {
                            await SendMessageAsync(chatId, "У вас нет прав завершать заявки.", Keyboards.MainMenu);
                            continue;
                        }

                        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parts.Length != 2 || !long.TryParse(parts[1], out var appId))
                        {
                            await SendMessageAsync(chatId, "Использование: /complete <id>", Keyboards.MainMenu);
                            continue;
                        }

                        var completed = _store.CompleteApplication(appId);
                        await SendMessageAsync(chatId,
                            completed ? $"Заявка #{appId} завершена." : $"Активная заявка #{appId} не найдена.",
                            Keyboards.MainMenu);
                        continue;
                    }

                    if (!_sessions.TryGetValue(userId, out var session))
                    {
                        await SendMessageAsync(chatId, "Не понял команду. Нажмите /start", Keyboards.MainMenu);
                        continue;
                    }

                    var nextPrompt = session.Handle(text);
                    if (nextPrompt is null && session.Step == "done")
                    {
                        var appId = _store.AddApplication(userId, session.Data);
                        var app = _store.GetById(appId);
                        _sessions.Remove(userId);
                        await SendMessageAsync(chatId, $"Заявка создана!\n\n{app.FormatCard()}", Keyboards.MainMenu);
                    }
                    else
                    {
                        await SendMessageAsync(chatId, nextPrompt ?? "Продолжайте", Keyboards.ForStep(session.Step));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    private async Task<List<Update>> GetUpdatesAsync()
    {
        var url = string.Format(ApiTemplate, _token, "getUpdates") + $"?timeout=25&offset={_offset}";
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<TgResponse<List<Update>>>(body);
        return parsed?.Result ?? [];
    }

    private async Task SendMessageAsync(long chatId, string text, object keyboard)
    {
        var url = string.Format(ApiTemplate, _token, "sendMessage");
        var payload = new
        {
            chat_id = chatId,
            text,
            reply_markup = keyboard
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }
}

sealed class SessionState(string step)
{
    public string Step { get; private set; } = step;
    public Dictionary<string, string> Data { get; } = new();

    public string? Handle(string text)
    {
        switch (Step)
        {
            case "pilot_type":
                if (text is not ("КТ" or "СТ")) return "Выберите тип кнопкой: КТ или СТ";
                Data["pilot_type"] = text;
                Step = "callsign";
                return "Напиши свой позывной:";

            case "callsign":
                if (string.IsNullOrWhiteSpace(text)) return "Позывной не может быть пустым. Введите позывной:";
                Data["callsign"] = text.Trim();
                Step = "daypart";
                return "Выбери время суток:";

            case "daypart":
                if (text is not ("День" or "Ночь")) return "Выберите кнопкой: День или Ночь";
                Data["daypart"] = text;
                Step = "control_frequency";
                return "Какая у тебя частота управления?";

            case "control_frequency":
                if (text is not ("500" or "900" or "2400")) return "Выберите частоту кнопкой: 500, 900 или 2400";
                Data["control_frequency"] = text;
                Step = "video_frequency";
                return "Какая у тебя частота видео?";

            case "video_frequency":
                if (text is not ("5.8" or "3.3" or "1.2")) return "Выберите частоту кнопкой: 5.8, 3.3 или 1.2";
                Data["video_frequency"] = text;
                Step = "bind_phrase";
                return "Введи BIND-фразу:";

            case "bind_phrase":
                if (string.IsNullOrWhiteSpace(text)) return "BIND-фраза не может быть пустой. Введите значение:";
                Data["bind_phrase"] = text.Trim();
                Step = "note";
                return "Примечание (по желанию). Можно отправить '-' чтобы оставить пустым:";

            case "note":
                Data["note"] = text.Trim() == "-" ? string.Empty : text.Trim();
                Step = "done";
                return null;

            default:
                return "Ошибка состояния. Нажмите «Оставить заявку» и попробуйте снова.";
        }
    }
}

static class Keyboards
{
    public static object MainMenu => Keyboard([ ["Активные заявки", "Завершенные заявки"], ["Оставить заявку"] ]);
    public static object PilotType => Keyboard([ ["КТ", "СТ"] ]);
    public static object DayPart => Keyboard([ ["День", "Ночь"] ]);
    public static object ControlFrequency => Keyboard([ ["500", "900", "2400"] ]);
    public static object VideoFrequency => Keyboard([ ["5.8", "3.3", "1.2"] ]);

    public static object ForStep(string step) => step switch
    {
        "pilot_type" => PilotType,
        "daypart" => DayPart,
        "control_frequency" => ControlFrequency,
        "video_frequency" => VideoFrequency,
        _ => MainMenu
    };

    private static object Keyboard(string[][] rows)
    {
        return new
        {
            keyboard = rows.Select(r => r.Select(text => new { text }).ToArray()).ToArray(),
            resize_keyboard = true,
            one_time_keyboard = false
        };
    }
}

sealed class ApplicationStore
{
    private readonly string _connectionString;

    public ApplicationStore(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        Init();
    }

    public long AddApplication(long telegramId, Dictionary<string, string> payload)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO applications (
    telegram_id, created_at, callsign, pilot_type, daypart,
    control_frequency, video_frequency, bind_phrase, note, status
)
VALUES (
    $telegram_id, $created_at, $callsign, $pilot_type, $daypart,
    $control_frequency, $video_frequency, $bind_phrase, $note, 'active'
);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$telegram_id", telegramId);
        cmd.Parameters.AddWithValue("$created_at", DateTime.Now.ToString("s"));
        cmd.Parameters.AddWithValue("$callsign", payload["callsign"]);
        cmd.Parameters.AddWithValue("$pilot_type", payload["pilot_type"]);
        cmd.Parameters.AddWithValue("$daypart", payload["daypart"]);
        cmd.Parameters.AddWithValue("$control_frequency", payload["control_frequency"]);
        cmd.Parameters.AddWithValue("$video_frequency", payload["video_frequency"]);
        cmd.Parameters.AddWithValue("$bind_phrase", payload["bind_phrase"]);
        cmd.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(payload.GetValueOrDefault("note")) ? "-" : payload["note"]);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public List<Application> GetApplications(string status)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM applications WHERE status = $status ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$status", status);
        using var reader = cmd.ExecuteReader();
        var result = new List<Application>();

        while (reader.Read())
        {
            result.Add(Application.FromReader(reader));
        }

        return result;
    }

    public bool CompleteApplication(long appId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE applications
SET status = 'completed', completed_at = $completed_at
WHERE id = $id AND status = 'active';";
        cmd.Parameters.AddWithValue("$completed_at", DateTime.Now.ToString("s"));
        cmd.Parameters.AddWithValue("$id", appId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public Application GetById(long appId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM applications WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", appId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException($"Заявка {appId} не найдена");
        return Application.FromReader(reader);
    }

    private void Init()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS applications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    telegram_id INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    completed_at TEXT,
    callsign TEXT NOT NULL,
    pilot_type TEXT NOT NULL,
    daypart TEXT NOT NULL,
    control_frequency TEXT NOT NULL,
    video_frequency TEXT NOT NULL,
    bind_phrase TEXT NOT NULL,
    note TEXT,
    status TEXT NOT NULL CHECK(status IN ('active', 'completed'))
);";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}

record Application(
    long Id,
    long TelegramId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string Callsign,
    string PilotType,
    string Daypart,
    string ControlFrequency,
    string VideoFrequency,
    string BindPhrase,
    string Note,
    string Status)
{
    public static Application FromReader(SqliteDataReader reader)
    {
        return new Application(
            reader.GetInt64(reader.GetOrdinal("id")),
            reader.GetInt64(reader.GetOrdinal("telegram_id")),
            DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("completed_at"))),
            reader.GetString(reader.GetOrdinal("callsign")),
            reader.GetString(reader.GetOrdinal("pilot_type")),
            reader.GetString(reader.GetOrdinal("daypart")),
            reader.GetString(reader.GetOrdinal("control_frequency")),
            reader.GetString(reader.GetOrdinal("video_frequency")),
            reader.GetString(reader.GetOrdinal("bind_phrase")),
            reader.IsDBNull(reader.GetOrdinal("note")) ? "-" : reader.GetString(reader.GetOrdinal("note")),
            reader.GetString(reader.GetOrdinal("status"))
        );
    }

    public string FormatCard()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ID: {Id}");
        sb.AppendLine($"Дата: {CreatedAt:dd.MM.yyyy}");
        sb.AppendLine($"Время: {CreatedAt:HH:mm:ss}");
        sb.AppendLine($"Позывной: {Callsign}");
        sb.AppendLine($"Тип: {PilotType}");
        sb.AppendLine($"Время суток: {Daypart}");
        sb.AppendLine($"Частота управления: {ControlFrequency}");
        sb.AppendLine($"Частота видео: {VideoFrequency}");
        sb.AppendLine($"BIND-фраза: {BindPhrase}");
        sb.AppendLine($"Примечание: {Note}");

        if (Status == "completed" && CompletedAt.HasValue)
        {
            sb.AppendLine($"Завершено: {CompletedAt:dd.MM.yyyy HH:mm:ss}");
        }

        return sb.ToString().TrimEnd();
    }
}

record TgResponse<T>(bool Ok, T Result);
record Update(int UpdateId, Message? Message);
record Message(long MessageId, Chat Chat, User? From, string? Text);
record Chat(long Id);
record User(long Id);
