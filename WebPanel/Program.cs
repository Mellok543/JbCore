using Microsoft.EntityFrameworkCore;
using WebPanel.Data;
using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var provider = builder.Configuration["Storage:Provider"]?.Trim().ToLowerInvariant();
if (provider == "postgres")
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Storage:Provider=postgres, but ConnectionStrings:Postgres is not configured.");
    }

    builder.Services.AddDbContext<AdminDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IAdminDataService, DbAdminDataService>();
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

if (provider == "postgres")
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
    storage = provider == "postgres" ? "postgres" : "excel",
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
