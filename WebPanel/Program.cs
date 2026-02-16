using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebPanel.Data;
using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var requestedProvider = builder.Configuration["Storage:Provider"]?.Trim().ToLowerInvariant();
var allowFallbackToExcel = builder.Configuration.GetValue("Storage:AllowFallbackToExcel", true);
var runtimeProvider = requestedProvider == "postgres" ? "postgres" : "excel";

if (requestedProvider == "postgres")
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Storage:Provider=postgres, but ConnectionStrings:Postgres is not configured.");
    }

    if (TryOpenPostgres(connectionString, out var error))
    {
        builder.Services.AddDbContext<AdminDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddScoped<IAdminDataService, DbAdminDataService>();
    }
    else if (allowFallbackToExcel)
    {
        runtimeProvider = "excel";
        builder.Services.AddSingleton<IAdminDataService, ExcelAdminService>();
        Console.WriteLine($"[WebPanel] PostgreSQL недоступен ({error}). Переключаюсь на Excel-режим.");
    }
    else
    {
        throw new InvalidOperationException(
            $"Не удалось подключиться к PostgreSQL: {error}. " +
            "Проверьте, что сервер БД запущен и строка подключения корректна, " +
            "или включите Storage:AllowFallbackToExcel=true.");
    }
}
else
{
    builder.Services.AddSingleton<IAdminDataService, ExcelAdminService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (runtimeProvider == "postgres")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    db.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "RepairBot WebPanel",
    storage = runtimeProvider,
    requestedStorage = requestedProvider == "postgres" ? "postgres" : "excel",
    fallbackEnabled = allowFallbackToExcel,
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

app.MapRazorPages();

app.Run();

static bool TryOpenPostgres(string connectionString, out string error)
{
    try
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        error = string.Empty;
        return true;
    }
    catch (Exception ex)
    {
        error = ex.Message;
        return false;
    }
}
