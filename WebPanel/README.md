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
- Дашборд по 3 категориям заявок (активные/завершённые).
- Список заявок с фильтрами (категория, статус), поиском и сменой статуса.
- Управление доступом (модерация рекомендаций + редактирование матрицы прав пользователей).
- Техподдержка: создание обращений, просмотр и смена статуса тикетов (`support.xlsx`).

## Конфигурация

### Excel-файлы
- `ExcelSync:TablesDirectory`
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

## Как выдавать доступ к сайту
1. Войдите под админом.
2. Откройте страницу **Пользователи** (`/Users`).
3. Создайте персональный логин/пароль для участника.
4. Передайте человеку его личные данные для входа.

## API
- `GET /api/health` → `storage = "excel"`
- `GET /api/dashboard` (требует авторизацию)
- `POST /api/sync/excel-to-db` (требует авторизацию, no-op)

## Если на сайте нет данных
1. Проверьте путь `ExcelSync:TablesDirectory`.
2. Убедитесь, что файлы существуют в этой папке.
3. Проверьте права на чтение/запись у пользователя `webpanel.service`.
4. Проверьте health:

```bash
curl -s http://127.0.0.1:8080/api/health
```

Ожидаемо: `storage = excel`.
