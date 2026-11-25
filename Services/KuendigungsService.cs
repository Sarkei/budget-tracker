using System.Text;
using System.Text.Json;
using BudgetTracker.Data;
using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

/// <summary>
/// Bereitet Kündigungen vor und legt sie nach Freigabe in die Mail-Warteschlange.
///
/// Der Ablauf ist bewusst dreistufig:
///   1. <see cref="VorbereitenAsync"/> baut den Text und speichert ihn als
///      Entwurf. Es geht nichts raus.
///   2. Tim liest den Text in der App und ändert ihn bei Bedarf.
///   3. <see cref="FreigebenAsync"/> legt die Mail in die Warteschlange; der
///      Cronjob `budget-mail-queue.py` verschickt sie über das Gmail-Konto.
///
/// Warum nicht automatisch: Eine Kündigung ist nicht umkehrbar. Ein falscher
/// Empfänger oder ein Ratenkauf, der als Abo gelesen wird, kostet Geld oder
/// Vertrauen. Deshalb bleibt der letzte Schritt beim Menschen — genau wie bei
/// Überweisungen.
/// </summary>
public class KuendigungsService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly string _mailDir;
    private readonly string _absenderName;
    private readonly string _absenderEmail;

    public KuendigungsService(IDbContextFactory<AppDbContext> factory, IConfiguration config)
    {
        _factory = factory;
        var data = Environment.GetEnvironmentVariable("DATA_PATH") ?? "/app/data";
        _mailDir = Path.Combine(data, "mailqueue");
        _absenderName = (config["Owner:Name"] ?? "Tim Nagraszus").Trim();
        _absenderEmail = (Environment.GetEnvironmentVariable("OWNER_EMAIL") ?? "").Trim();
    }

    /// <summary>
    /// Prüft, ob ein Posten überhaupt kündbar ist.
    ///
    /// Ratenkäufe ausdrücklich ausgenommen — Tims Hinweis: „aufpassen das
    /// ratenkäufe keine abos sind die man kündigen kann". Eine Rate ist eine
    /// Schuld, kein Abo; sie endet mit der letzten Zahlung.
    /// </summary>
    public (bool Kuendbar, string Grund) IstKuendbar(AboPosten abo, AboAnbieter? anbieter)
        => Kuendigungsregel.Pruefe(abo, anbieter);

    /// <summary>
    /// Baut den Kündigungstext.
    ///
    /// Formell und höflich, mit den Angaben, die ein Anbieter zur Zuordnung
    /// braucht: Name, Anschrift-Platzhalter (Tim ergänzt), betroffenes Abo und
    /// der Wunsch nach schriftlicher Bestätigung. Der Text wird nicht
    /// abgeschickt — er ist ein Entwurf.
    /// </summary>
    public Kuendigung Entwerfen(AboPosten abo, AboAnbieter? anbieter, int userId, decimal betragProMonat)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sehr geehrte Damen und Herren,");
        sb.AppendLine();
        sb.AppendLine($"hiermit kündige ich mein Abonnement '{abo.Name}'"
                      + (betragProMonat > 0 ? $" (monatlich {betragProMonat:F2} €)" : "")
                      + " fristgerecht zum nächstmöglichen Termin.");
        sb.AppendLine();
        sb.AppendLine("Bitte bestätigen Sie mir die Kündigung sowie das Vertragsende "
                      + "schriftlich per E-Mail.");
        sb.AppendLine();
        sb.AppendLine("Sollte eine Kündigung über dieses Schreiben nicht möglich sein, "
                      + "teilen Sie mir bitte den korrekten Weg mit.");
        sb.AppendLine();
        sb.AppendLine("Mit freundlichen Grüßen");
        sb.AppendLine(_absenderName);

        return new Kuendigung
        {
            AboName = abo.Name,
            Empfaenger = anbieter?.Email,
            Betreff = $"Kündigung meines Abonnements '{abo.Name}'",
            Text = sb.ToString(),
            Status = KuendigungsStatus.Vorbereitet,
            BetragProMonat = betragProMonat,
            ErstelltAm = DateTime.UtcNow,
            UserId = userId,
            Hinweis = anbieter == null
                ? "Kein E-Mail-Weg bekannt — bitte im Kundenkonto kündigen."
                : anbieter.Kuendigungsweg,
        };
    }

    /// <summary>Entwurf speichern (noch kein Versand).</summary>
    public async Task<Kuendigung> SpeichernAsync(Kuendigung k)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Kuendigungen.Add(k);
        await db.SaveChangesAsync();
        return k;
    }

    /// <summary>
    /// Freigeben: Mail in die Warteschlange legen.
    ///
    /// Erst hier verlässt der Text die App. Der Cronjob verschickt JSON-Dateien
    /// aus `mailqueue` — dasselbe Verfahren wie beim Passwort-Reset, also ohne
    /// Mail-Passwort im Container.
    /// </summary>
    public async Task<(bool Ok, string Meldung)> FreigebenAsync(int kuendigungId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var k = await db.Kuendigungen.FirstOrDefaultAsync(x => x.Id == kuendigungId);
        if (k == null) return (false, "Kündigung nicht gefunden.");
        if (k.Status == KuendigungsStatus.Versendet) return (false, "Bereits versendet.");
        if (string.IsNullOrWhiteSpace(k.Empfaenger))
            return (false, "Kein Empfänger hinterlegt — bitte Adresse eintragen.");
        if (string.IsNullOrWhiteSpace(k.Text))
            return (false, "Kein Text vorhanden.");

        // Schutz: Ratenkäufe gehen nie per Mail raus.
        if (Kuendigungsregel.IstRatenkauf(k.AboName))
            return (false, "Ratenzahlung — nicht kündbar, keine Mail versendet.");

        Directory.CreateDirectory(_mailDir);
        var payload = new
        {
            to = k.Empfaenger,
            subject = k.Betreff ?? $"Kündigung {k.AboName}",
            html = k.Text!.Replace("\n", "<br>"),
        };
        var datei = Path.Combine(_mailDir, $"{DateTime.UtcNow:yyyyMMddHHmmss}-k-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(datei, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        k.Status = KuendigungsStatus.Versendet;
        k.VersendetAm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (true, $"Kündigung an {k.Empfaenger} in die Warteschlange gelegt.");
    }

    /// <summary>Zurückziehen (falls Tim es sich anders überlegt).</summary>
    public async Task VerwerfenAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var k = await db.Kuendigungen.FirstOrDefaultAsync(x => x.Id == id);
        if (k == null) return;
        k.Status = KuendigungsStatus.Verworfen;
        await db.SaveChangesAsync();
    }

    /// <summary>Alle Kündigungen eines Nutzers.</summary>
    public async Task<List<Kuendigung>> ListeAsync(int userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Kuendigungen
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.ErstelltAm)
            .ToListAsync();
    }
}
