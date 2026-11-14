using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BudgetTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

/// <summary>
/// Passwort-Reset: erzeugt ein einmaliges Token (1 Stunde gültig), legt eine Mail in
/// die Warteschlange und setzt nach Klick auf den Link das neue Passwort.
/// Der Versand passiert nicht hier, sondern ueber den Agenten (Gmail) - so liegt
/// kein Mail-Passwort im Container.
/// </summary>
public class PasswordResetService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly string _mailDir;

    public PasswordResetService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        var data = Environment.GetEnvironmentVariable("DATA_PATH") ?? "/app/data";
        _mailDir = Path.Combine(data, "mailqueue");
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Legt ein Token an und schreibt die Mail. Gibt das Token zurück (nur zum Testen).</summary>
    public async Task<string?> CreateAsync(string email)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var nutzer = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == email.ToLower() && u.IsActive);
        if (nutzer == null) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"PasswordResets\" (\"UserId\", \"TokenHash\", \"ExpiresAt\", \"UsedAt\") VALUES ({0}, {1}, {2}, NULL)",
            nutzer.Id, Hash(token), DateTime.UtcNow.AddHours(1));

        Directory.CreateDirectory(_mailDir);
        var baseUrl = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "https://budget-tracker.nas-timgreen.com").TrimEnd('/');
        var link = $"{baseUrl}/reset?token={Uri.EscapeDataString(token)}";

        var mail = new
        {
            to = nutzer.Username,
            subject = "Budget Tracker – Passwort zurücksetzen",
            html =
                $"<p>Hallo {nutzer.FirstName},</p>" +
                "<p>du hast ein neues Passwort angefordert. Der Link ist eine Stunde gültig und nur einmal verwendbar:</p>" +
                $"<p><a href=\"{link}\">{link}</a></p>" +
                "<p>Wenn du das nicht warst, ignoriere diese Mail – dein Passwort bleibt unverändert.</p>"
        };
        var datei = Path.Combine(_mailDir, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(datei, JsonSerializer.Serialize(mail, new JsonSerializerOptions { WriteIndented = true }));

        return token;
    }

    /// <summary>Prüft das Token, ohne es zu verbrauchen.</summary>
    public async Task<bool> IsValidAsync(string token)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var hash = Hash(token);
        return await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM \"PasswordResets\" WHERE \"TokenHash\" = {hash} AND \"UsedAt\" IS NULL AND \"ExpiresAt\" > {DateTime.UtcNow}")
            .FirstOrDefaultAsync() > 0;
    }

    /// <summary>Setzt das Passwort und entwertet das Token.</summary>
    public async Task<bool> ResetAsync(string token, string neuesPasswort)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var hash = Hash(token);

        var zeile = await db.Database
            .SqlQuery<int>($"SELECT \"UserId\" AS \"Value\" FROM \"PasswordResets\" WHERE \"TokenHash\" = {hash} AND \"UsedAt\" IS NULL AND \"ExpiresAt\" > {DateTime.UtcNow}")
            .FirstOrDefaultAsync();

        if (zeile == 0) return false;

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"PasswordHash\" = {0} WHERE \"Id\" = {1}",
            BCrypt.Net.BCrypt.HashPassword(neuesPasswort), zeile);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"PasswordResets\" SET \"UsedAt\" = {0} WHERE \"TokenHash\" = {1}", DateTime.UtcNow, hash);
        return true;
    }
}
