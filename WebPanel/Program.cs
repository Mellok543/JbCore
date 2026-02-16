using WebPanel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<ExcelAdminService>();

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
    utc = DateTime.UtcNow
}));

app.MapGet("/api/roadmap", () => Results.Ok(new[]
{
    "Dashboard (active/completed requests)",
    "Recommendations moderation",
    "Access/roles management",
    "FAQ and support tickets"
}));

app.MapGet("/api/dashboard", (ExcelAdminService service) => Results.Ok(service.GetDashboard()));

app.MapRazorPages();

app.Run();
