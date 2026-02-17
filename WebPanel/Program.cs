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
    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw("ALTER TABLE IF EXISTS requests ADD COLUMN IF NOT EXISTS external_id bigint");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS idx_requests_category_status ON requests (category, status)");
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
