# WebPanel

WebPanel — веб-интерфейс для администрирования Telegram-бота.

## Выбранный режим синхронизации
Проект переведён в **single source of truth = Excel**:
- бот пишет в Excel,
- WebPanel читает напрямую те же Excel-файлы,
- промежуточная синхронизация в PostgreSQL не используется.

Это убирает рассинхрон и сложности деплоя БД.

## Конфигурация
Настройки Excel-файлов:

- `ExcelSync:TablesDirectory`
- `ExcelSync:ApplicationsFileName`
- `ExcelSync:RepairsFileName`
- `ExcelSync:ConsumablesFileName`
- `ExcelSync:AccessFileName`

Пример:

```json
{
  "ExcelSync": {
    "TablesDirectory": "../data",
    "ApplicationsFileName": "applications.xlsx",
    "RepairsFileName": "repairs.xlsx",
    "ConsumablesFileName": "consumables.xlsx",
    "AccessFileName": "access_users.xlsx"
  }
}
```

## API
- `GET /api/health` → `storage = "excel"`
- `GET /api/dashboard`
- `POST /api/sync/excel-to-db` → no-op (оставлен для обратной совместимости с ботом)

## Если на сайте нет данных
1. Проверьте путь `ExcelSync:TablesDirectory`.
2. Убедитесь, что файлы существуют в этой папке.
3. Проверьте права на чтение у пользователя, под которым запущен `webpanel.service`.
4. Выполните:

```bash
curl -s http://127.0.0.1:8080/api/health
```

и убедитесь, что `storage` = `excel`.
