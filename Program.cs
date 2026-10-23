using ApexCharts;
using BudgetTracker.Data;
using BudgetTracker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DetailedErrors"));

builder.Services.AddMudServices();
builder.Services.AddApexCharts();

// Configure SQLite with Docker-volume-aware path
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    // DATA_PATH env var set in Docker; fallback to local ./data folder for dev
    var dataPath = Environment.GetEnvironmentVariable("DATA_PATH")
        ?? Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(dataPath);
    var dbPath = Path.Combine(dataPath, "budget.db");
    options.UseSqlite($"Data Source={dbPath}");
});

// Persist DataProtection keys to data volume so they survive container restarts
var dpDataPath = Environment.GetEnvironmentVariable("DATA_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dpDataPath, "dp-keys")))
    .SetApplicationName("BudgetTracker");

// Cookie-based authentication (no ASP.NET Identity – simple custom user table)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "BudgetTracker.Auth";
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Razor Pages needed for login/logout/register endpoints
builder.Services.AddRazorPages();

builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<CamtCsvImportService>();
builder.Services.AddScoped<GroupCsvImportService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddSingleton<GroupNotificationService>();

var app = builder.Build();

// Initialize / seed database at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    db.Database.EnsureCreated();
    DatabaseInitializer.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapRazorComponents<BudgetTracker.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
