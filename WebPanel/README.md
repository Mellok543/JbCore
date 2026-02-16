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
