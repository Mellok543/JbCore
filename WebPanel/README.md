# WebPanel

WebPanel — веб-интерфейс для администрирования Telegram-бота.

## Где указывать путь к Excel таблицам
Есть два основных способа:

1. В `WebPanel/appsettings.json`:
   - `TablesDirectory` — папка с таблицами
   - `ApplicationsFileName`, `RepairsFileName`, `ConsumablesFileName`, `AccessFileName` — имена Excel файлов
2. Через переменные окружения (приоритетнее, удобно для сервера/systemd):
   - `TablesDirectory=/opt/bot/tables`

Пример запуска в Excel-режиме:

```bash
Storage__Provider=excel TablesDirectory=/opt/bot/tables dotnet run --project WebPanel/WebPanel.csproj
```

---

## Начало перехода на настоящую БД (PostgreSQL)
В проекте уже добавлена начальная инфраструктура:

- `IAdminDataService` — единый интерфейс источника данных.
- `ExcelAdminService` — текущая работа с Excel.
- `DbAdminDataService` + `AdminDbContext` — новая реализация для PostgreSQL.
- Переключение режима через `Storage:Provider`:
  - `excel` (по умолчанию)
  - `postgres`

Пример запуска в Postgres-режиме:

```bash
Storage__Provider=postgres \
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=repairbot;Username=repairbot;Password=repairbot" \
dotnet run --project WebPanel/WebPanel.csproj
```

При старте в `postgres` режиме используется `EnsureCreated()` для создания таблиц.

---

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

## Следующие шаги по миграции на БД
1. Добавить EF Core migrations вместо `EnsureCreated`.
2. Сделать импорт данных Excel -> PostgreSQL (one-time script).
3. Переключить бота на общий PostgreSQL источник (или API) вместе с WebPanel.
4. Добавить авторизацию/роли для веб-панели.

## Важно для Windows (ошибка `Invalid JSON`)
Если указываете путь в `appsettings.json`, нельзя использовать неэкранированные `\` в JSON-строке.

Неправильно:
```json
"TablesDirectory": "E:\RiderProjects\CreateBot And Web\tables"
```

Правильно (любой вариант):
```json
"TablesDirectory": "E:/RiderProjects/CreateBot And Web/tables"
```
или
```json
"TablesDirectory": "E:\\RiderProjects\\CreateBot And Web\\tables"
```

Проще и безопаснее на Windows задавать путь через переменную окружения:
```powershell
$env:TablesDirectory = "E:/RiderProjects/CreateBot And Web/tables"
dotnet run --project WebPanel/WebPanel.csproj
```

## Ошибка подключения к PostgreSQL (`10061`, connection refused)
Если видите ошибку вида `Failed to connect to 127.0.0.1:5432`:

1. Убедитесь, что PostgreSQL реально запущен и слушает порт `5432`.
2. Проверьте `ConnectionStrings:Postgres` (host/port/db/user/password).
3. В Rider откройте Database и проверьте подключение теми же параметрами.

Для более мягкого запуска добавлен флаг:

```json
"Storage": {
  "Provider": "postgres",
  "AllowFallbackToExcel": true
}
```

Если PostgreSQL недоступен, WebPanel автоматически перейдет на Excel-режим и не упадет.

Если хотите строгий режим (без fallback), установите:

```json
"Storage": {
  "Provider": "postgres",
  "AllowFallbackToExcel": false
}
```

Тогда приложение завершится с понятной ошибкой, если БД не поднята.
