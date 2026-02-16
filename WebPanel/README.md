# WebPanel

WebPanel — это веб-интерфейс для администрирования Telegram-бота.

## Что реализовано сейчас
- Дашборд со сводкой по заявкам (дроны / ремонт / комплектующие) и pending-рекомендациям.
- Отдельная страница заявок с фильтрами по категории и статусу.
- Страница управления доступом:
  - просмотр пользователей и их прав;
  - модерация рекомендаций (принять/отклонить) с записью результата в Excel.
- API endpoints:
  - `GET /api/health`
  - `GET /api/roadmap`
  - `GET /api/dashboard`

## Конфигурация
По умолчанию WebPanel читает Excel-файлы из директории запуска приложения.
Можно переопределить через переменные окружения:

- `TablesDirectory` (папка с таблицами)
- `ApplicationsFileName` (по умолчанию `applications.xlsx`)
- `RepairsFileName` (по умолчанию `repairs.xlsx`)
- `ConsumablesFileName` (по умолчанию `consumables.xlsx`)
- `AccessFileName` (по умолчанию `access_users.xlsx`)

Пример:

```bash
TablesDirectory=/opt/bot/tables dotnet run --project WebPanel/WebPanel.csproj
```

## Следующие шаги
1. Добавить авторизацию и роли в WebPanel.
2. Перенести данные с Excel на PostgreSQL и дать боту + вебу общий источник.
3. Добавить редактирование прав напрямую из WebPanel.
