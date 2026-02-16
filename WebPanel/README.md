# WebPanel

WebPanel — веб-интерфейс для администрирования Telegram-бота.

## Режим хранения
WebPanel теперь работает **только с PostgreSQL** (Excel-режим полностью удалён).

## Конфигурация
Настроить нужно только строку подключения:

- `ConnectionStrings:Postgres`

Пример в `WebPanel/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=repairbot;Username=repairbot;Password=repairbot"
  }
}
```

Пример запуска через переменные окружения:

```bash
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=repairbot;Username=repairbot;Password=repairbot" \
dotnet run --project WebPanel/WebPanel.csproj
```

## Что создаётся в БД
При старте приложения вызывается `EnsureCreated()`, и создаются таблицы:

- `requests`
- `users`
- `recommendations`

## Ошибка подключения к PostgreSQL
Если WebPanel не стартует, проверьте:

1. PostgreSQL запущен и слушает нужный порт.
2. Пользователь/пароль в `ConnectionStrings:Postgres` верные.
3. Есть доступ к базе `repairbot`.

### Частая ошибка: `28P01 password authentication failed`
Это означает, что пароль/пользователь в строке подключения не совпадает с PostgreSQL.

Исправление на сервере:

```bash
sudo -u postgres psql -c "ALTER USER repairbot WITH PASSWORD 'НОВЫЙ_ПАРОЛЬ';"
```

После этого обновите пароль в env/appsettings и перезапустите `webpanel.service`.

## Что реализовано сейчас
- Дашборд со сводкой по заявкам и pending-рекомендациям.
- Страница заявок с фильтрами по категории/статусу.
- Страница управления доступом:
  - просмотр пользователей и прав,
  - модерация рекомендаций (принять/отклонить).
- API endpoints:
  - `GET /api/health`
  - `GET /api/roadmap`
  - `GET /api/dashboard`

## Следующие шаги
1. Добавить EF Core migrations вместо `EnsureCreated`.
2. Реализовать импорт legacy данных из Excel (one-time script), если ещё нужен.
3. Переключить бота на общий PostgreSQL источник (или API).
