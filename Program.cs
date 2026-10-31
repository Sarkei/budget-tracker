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
builder.Services.AddScoped<BankImportService>();
builder.Services.AddHostedService<BankSyncWorker>();
builder.Services.AddScoped<GroupCsvImportService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddSingleton<GroupNotificationService>();

builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    // Hinter Cloudflare kommt HTTPS als X-Forwarded-Proto an - ohne das baut die App http-Links.
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Initialize / seed database at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    db.Database.EnsureCreated();
    DatabaseInitializer.Initialize(db);
}

// Bankumsaetze einlesen, sobald ein Datenordner eingehaengt ist (idempotent).
var bankPath = Environment.GetEnvironmentVariable("BANK_DATA_PATH");
if (!string.IsNullOrWhiteSpace(bankPath) && Directory.Exists(bankPath))
{
    await using var bankScope = app.Services.CreateAsyncScope();
    var importer = bankScope.ServiceProvider.GetRequiredService<BankImportService>();
    var ergebnis = await importer.ImportAsync();
    Console.WriteLine($"[Bankimport] {ergebnis.Summary}");
    foreach (var m in ergebnis.Messages) Console.WriteLine($"[Bankimport] {m}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseForwardedHeaders();
app.UseAuthentication();

// Finanzdaten sieht ausschliesslich der Besitzer (OWNER_EMAIL, automatisch Admin).
// Alle anderen Konten koennen sich anmelden, bekommen aber keine Zahlen zu sehen.
app.Use(async (ctx, next) =>
{
    var pfad = ctx.Request.Path.Value ?? "/";
    var oeffentlich = pfad.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/register", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/logout", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/_", StringComparison.Ordinal)
        || pfad.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
        || pfad.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);

    if (!oeffentlich && ctx.User.Identity?.IsAuthenticated == true && !ctx.User.IsInRole("Admin"))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(
            "<!doctype html><meta charset=\"utf-8\"><title>Kein Zugriff</title>" +
            "<body style=\"font-family:system-ui;max-width:38rem;margin:4rem auto;padding:0 1rem\">" +
            "<h1>Kein Zugriff</h1><p>Dieses Konto hat keine Freigabe fuer die Finanzdaten. " +
            "Die Daten gehoeren ausschliesslich dem Besitzer der Installation.</p>" +
            "<p><a href=\"/logout\">Abmelden</a></p></body>");
        return;
    }

    await next();
});

app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapRazorComponents<BudgetTracker.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
