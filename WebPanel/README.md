# WebPanel

WebPanel — веб-интерфейс для администрирования Telegram-бота.

## Режим хранения
WebPanel работает только с PostgreSQL.

## Конфигурация
Обязательная настройка:

- `ConnectionStrings:Postgres`

Опционально для импорта из Excel (бот может продолжать работать на Excel):

- `ExcelSync:RunOnStartup` — запускать синхронизацию при старте (`true/false`)
- `ExcelSync:TablesDirectory` — папка с Excel-файлами бота
- `ExcelSync:ApplicationsFileName`
- `ExcelSync:RepairsFileName`
- `ExcelSync:ConsumablesFileName`
- `ExcelSync:AccessFileName`

Пример `WebPanel/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=repairbot;Username=repairbot;Password=repairbot"
  },
  "ExcelSync": {
    "RunOnStartup": false,
    "TablesDirectory": "../",
    "ApplicationsFileName": "applications.xlsx",
    "RepairsFileName": "repairs.xlsx",
    "ConsumablesFileName": "consumables.xlsx",
    "AccessFileName": "access_users.xlsx"
  }
}
```

## Как переносить данные с Excel (бот остаётся на Excel)
1. Укажите `ExcelSync:TablesDirectory` на папку, где бот пишет файлы.
2. Запустите WebPanel.
3. Выполните импорт вручную:

```bash
curl -X POST http://127.0.0.1:8080/api/sync/excel-to-db
```

4. Проверьте метрики/списки в WebPanel.

### Что синхронизируется
- `applications.xlsx` (`Applications`) -> `requests` (`category=drone`)
- `repairs.xlsx` (`Repairs`) -> `requests` (`category=repair`)
- `consumables.xlsx` (`Consumables`) -> `requests` (`category=consumables`)
- `access_users.xlsx` (`Users`, `Recommendations`) -> `users`, `recommendations`

> Для исключения конфликтов `ID` между категориями используется внутренний ID в БД, а в UI показывается исходный ID из Excel.

## Что создаётся в БД
При старте приложения вызывается `EnsureCreated()`, и создаются таблицы:

- `requests`
- `users`
- `recommendations`

Также автоматически добавляется колонка `requests.external_id` (если отсутствует).

## Ошибка подключения к PostgreSQL
Если WebPanel не стартует, проверьте:

1. PostgreSQL запущен и слушает нужный порт.
2. Пользователь/пароль в `ConnectionStrings:Postgres` верные.
3. Есть доступ к базе `repairbot`.

### Частая ошибка: `28P01 password authentication failed`

```bash
sudo -u postgres psql -c "ALTER USER repairbot WITH PASSWORD 'НОВЫЙ_ПАРОЛЬ';"
```

После этого обновите пароль в env/appsettings и перезапустите `webpanel.service`.
