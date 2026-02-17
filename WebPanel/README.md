# WebPanel

WebPanel — закрытый веб-интерфейс команды по дронам и микроэлектронике.

## Выбранный режим синхронизации
Проект работает в режиме **single source of truth = Excel**:
- бот пишет в Excel,
- WebPanel читает/обновляет те же Excel-файлы,
- синхронизация через PostgreSQL отключена.

## Что есть в WebPanel сейчас
- Авторизация пользователей (cookie login).
- Создание пользователей сайта прямо из интерфейса (страница `/Users`, только для admin).
- Доска заявок по 3 категориям (активные/завершённые).
- Список заявок с фильтрами (категория, статус), поиском и сменой статуса.
- Управление доступом (модерация рекомендаций + редактирование матрицы прав пользователей).
- Техподдержка: создание обращений, просмотр и смена статуса тикетов (`support.xlsx`).

## Конфигурация

### Excel-файлы
- `ExcelSync:TablesDirectory` (рекомендуемый путь: `/opt/repairbot/tables`)
- `ExcelSync:ApplicationsFileName`
- `ExcelSync:RepairsFileName`
- `ExcelSync:ConsumablesFileName`
- `ExcelSync:AccessFileName`
- `ExcelSync:SupportFileName`

### Авторизация
WebPanel хранит пользователей сайта в Excel-файле `Auth:UsersFileName` (по умолчанию `site_users.xlsx`).

Параметры bootstrap-админа:
- `Auth:BootstrapAdminUsername`
- `Auth:BootstrapAdminPassword`
- `Auth:BootstrapAdminDisplayName`

При первом запуске bootstrap-admin добавляется автоматически в `site_users.xlsx`.
**Обязательно смените дефолтный пароль `change_me` перед production-запуском.**

## Роли доступа
- `operator` — только просмотр страниц, без изменений данных.
- `admin` — полный доступ: управление правами, статусами, обращениями и пользователями сайта.

## Как выдавать доступ к сайту
1. Войдите под админом.
2. Откройте страницу **Пользователи** (`/Users`).
3. Создайте персональный логин/пароль для участника.
4. Передайте человеку его личные данные для входа.


## Уведомления в Telegram
Для новых обращений техподдержки можно включить отправку уведомлений в Telegram.

Параметры в `appsettings.json`:
- `TelegramNotifications:Enabled`
- `TelegramNotifications:BotToken`
- `TelegramNotifications:ChatIds` (список через запятую, например `-100123,-100456`)

При создании обращения из WebPanel отправляется сообщение в указанные чаты.

## API
- `GET /api/health` → `storage = "excel"`
- `GET /api/dashboard` (требует авторизацию)
- `POST /api/sync/excel-to-db` (требует авторизацию, no-op)

## Если WebPanel не стартует с ошибкой JSON (0x09)
Если в `appsettings.json` случайно попал символ табуляции внутри строкового значения (часто в `BotToken`/`ChatIds`), приложение падало на старте.
Текущая версия автоматически экранирует такие табы при запуске.

Рекомендуется также вручную проверить и убрать скрытые символы в `/opt/repairbot/web/appsettings.json`.

## Если на сайте нет данных
1. Проверьте путь `ExcelSync:TablesDirectory` (должен быть `/opt/repairbot/tables`).
2. Убедитесь, что файлы существуют в этой папке.
3. Проверьте права на чтение/запись у пользователя `webpanel.service`.
4. Проверьте health:

```bash
curl -s http://127.0.0.1:8080/api/health
```

Ожидаемо: `storage = excel`.
