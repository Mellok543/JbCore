# WebPanel

WebPanel — веб-интерфейс для администрирования Telegram-бота.

## Выбранный режим синхронизации
Проект работает в режиме **single source of truth = Excel**:
- бот пишет в Excel,
- WebPanel читает/обновляет те же Excel-файлы,
- синхронизация через PostgreSQL отключена.

## Что есть в WebPanel сейчас
- Авторизация пользователей (cookie login).
- Дашборд по 3 категориям заявок (активные/завершённые).
- Список заявок с фильтрами (категория, статус), поиском и сменой статуса (активная/завершённая).
- Управление доступом (модерация рекомендаций + редактирование матрицы прав пользователей).
- Техподдержка: создание обращений, просмотр и смена статуса тикетов (`support.xlsx`).

## Конфигурация
Настройки файлов:

- `ExcelSync:TablesDirectory`
- `ExcelSync:ApplicationsFileName`
- `ExcelSync:RepairsFileName`
- `ExcelSync:ConsumablesFileName`
- `ExcelSync:AccessFileName`
- `ExcelSync:SupportFileName`

Пример:

```json
{
  "ExcelSync": {
    "TablesDirectory": "../data",
    "ApplicationsFileName": "applications.xlsx",
    "RepairsFileName": "repairs.xlsx",
    "ConsumablesFileName": "consumables.xlsx",
    "AccessFileName": "access_users.xlsx",
    "SupportFileName": "support.xlsx"
  }
}
```

## Авторизация
WebPanel использует cookie-авторизацию.

1. В `appsettings.json` задайте пользователей в секции `Auth:Users`.
2. При первом запуске обязательно смените пароль `change_me`.
3. После запуска откройте `/Login` и войдите под учетной записью из конфигурации.

Пример:

```json
"Auth": {
  "Users": [
    {
      "Username": "admin",
      "Password": "very_strong_password",
      "DisplayName": "Administrator",
      "Role": "admin"
    }
  ]
}
```

## API
- `GET /api/health` → `storage = "excel"`
- `GET /api/dashboard`
- `POST /api/sync/excel-to-db` → no-op (оставлен для обратной совместимости)

## Если на сайте нет данных
1. Проверьте путь `ExcelSync:TablesDirectory`.
2. Убедитесь, что файлы существуют в этой папке.
3. Проверьте права на чтение/запись у пользователя `webpanel.service`.
4. Проверьте health:

```bash
curl -s http://127.0.0.1:8080/api/health
```

Ожидаемо: `storage = excel`.
