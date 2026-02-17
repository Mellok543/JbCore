using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<ExcelSyncOptions>(builder.Configuration.GetSection(ExcelSyncOptions.SectionName));
builder.Services.AddScoped<IAdminDataService, ExcelAdminDataService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "RepairBot WebPanel",
    storage = "excel",
    utc = DateTime.UtcNow
}));

app.MapGet("/api/roadmap", () => Results.Ok(new[]
{
    "Single source storage: Excel",
    "Dashboard (active/completed requests)",
    "Recommendations moderation",
    "Access/roles management",
    "Support tickets"
}));

app.MapGet("/api/dashboard", (IAdminDataService service) => Results.Ok(service.GetDashboard()));
app.MapPost("/api/sync/excel-to-db", () => Results.Ok(new
{
    ok = true,
    mode = "excel-only",
    message = "WebPanel работает напрямую с Excel, отдельная синхронизация в БД не требуется."
}));

app.MapRazorPages();

app.Run();
