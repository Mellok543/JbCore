using Microsoft.AspNetCore.Authentication.Cookies;
using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Login");
        options.Conventions.AuthorizePage("/Users", "AdminOnly");
    });

builder.Services.Configure<ExcelSyncOptions>(builder.Configuration.GetSection(ExcelSyncOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddScoped<IAdminDataService, ExcelAdminDataService>();
builder.Services.AddSingleton<IAuthService, ExcelAuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

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
    "Support tickets",
    "Cookie authentication",
    "Site user management"
})).RequireAuthorization();

app.MapGet("/api/dashboard", (IAdminDataService service) => Results.Ok(service.GetDashboard())).RequireAuthorization();
app.MapPost("/api/sync/excel-to-db", () => Results.Ok(new
{
    ok = true,
    mode = "excel-only",
    message = "WebPanel работает напрямую с Excel, отдельная синхронизация в БД не требуется."
})).RequireAuthorization();

app.MapRazorPages();

app.Run();
