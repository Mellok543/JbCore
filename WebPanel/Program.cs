using Npgsql;
using Microsoft.EntityFrameworkCore;
using WebPanel.Data;
using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
}

builder.Services.Configure<ExcelSyncOptions>(builder.Configuration.GetSection(ExcelSyncOptions.SectionName));
builder.Services.AddDbContext<AdminDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAdminDataService, DbAdminDataService>();
builder.Services.AddScoped<ExcelToPostgresSyncService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

    try
    {
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("""ALTER TABLE IF EXISTS requests ADD COLUMN IF NOT EXISTS "ExternalId" bigint""");
        db.Database.ExecuteSqlRaw("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'requests' AND column_name = 'Category'
                ) AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'requests' AND column_name = 'Status'
                ) THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS idx_requests_category_status ON requests ("Category", "Status")';
                ELSIF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'requests' AND column_name = 'category'
                ) AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'requests' AND column_name = 'status'
                ) THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS idx_requests_category_status ON requests (category, status)';
                END IF;
            END $$;
            """);
    }
    catch (PostgresException ex) when (ex.SqlState == "28P01")
    {
        throw new InvalidOperationException(
            "Не удалось подключиться к PostgreSQL: ошибка авторизации пользователя/пароля (28P01). " +
            "Проверьте ConnectionStrings:Postgres или env ConnectionStrings__Postgres, затем перезапустите WebPanel.", ex);
    }
    catch (PostgresException ex) when (ex.SqlState == "42703")
    {
        throw new InvalidOperationException(
            "Ошибка схемы БД (42703): отсутствуют ожидаемые колонки в таблице requests. " +
            "Проверьте структуру таблицы requests или пересоздайте схему для WebPanel.", ex);
    }
    catch (NpgsqlException ex)
    {
        throw new InvalidOperationException(
            "Не удалось подключиться к PostgreSQL. Проверьте, что сервер доступен и строка подключения корректна.", ex);
    }
}

if (builder.Configuration.GetValue<bool>("ExcelSync:RunOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var sync = scope.ServiceProvider.GetRequiredService<ExcelToPostgresSyncService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupSync");
    var report = sync.Sync();
    logger.LogInformation("Excel -> PostgreSQL sync completed: {Report}", report);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "RepairBot WebPanel",
    storage = "postgres",
    utc = DateTime.UtcNow
}));

app.MapGet("/api/roadmap", () => Results.Ok(new[]
{
    "Dashboard (active/completed requests)",
    "Recommendations moderation",
    "Access/roles management",
    "FAQ and support tickets"
}));

app.MapGet("/api/dashboard", (IAdminDataService service) => Results.Ok(service.GetDashboard()));
app.MapPost("/api/sync/excel-to-db", (ExcelToPostgresSyncService sync) => Results.Ok(sync.Sync()));

app.MapRazorPages();

app.Run();
