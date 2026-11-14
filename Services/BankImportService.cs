using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BudgetTracker.Data;
using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

public class BankImportResult
{
    public int Accounts { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Transfers { get; set; }
    public List<string> Messages { get; } = new();

    public string Summary =>
        $"{Accounts} Konto/Konten, {Inserted} neu, {Updated} aktualisiert, {Skipped} unverändert, {Transfers} Umbuchungen erkannt";
}

/// <summary>
/// Liest die EnableBanking-Abrufe aus dem Datenordner (eine JSON je Konto-Profil)
/// und legt Konten und Umsätze an. Mehrfaches Einlesen erzeugt keine Dubletten,
/// weil jede Buchung über ihre Bank-Referenz erkannt wird.
/// </summary>
public class BankImportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<BankImportService> _log;
    private readonly string _dataPath;

    public BankImportService(IDbContextFactory<AppDbContext> factory, ILogger<BankImportService> log)
    {
        _factory = factory;
        _log = log;
        _dataPath = Environment.GetEnvironmentVariable("BANK_DATA_PATH") ?? "/data/bank";
    }

    /// <summary>Kontoname je Bankprofil, abgeleitet aus dem Dateinamen.</summary>
    private static (string Name, string Type, string Color, string Icon) ProfilFuer(string datei)
    {
        var name = Path.GetFileName(datei).ToLowerInvariant();
        if (name.Contains("paypal"))
            return ("PayPal", "PayPal", "#0079C1", "AccountBalanceWallet");
        if (name.Contains("sparkasse") || name.Contains("umsaetze") || name.Contains("historie"))
            return ("Sparkasse Girokonto", "Girokonto", "#E30613", "AccountBalance");
        return (Path.GetFileNameWithoutExtension(datei), "Konto", "#607D8B", "AccountBalance");
    }

    public async Task<BankImportResult> ImportAsync(CancellationToken ct = default)
    {
        var result = new BankImportResult();
        if (!Directory.Exists(_dataPath))
        {
            result.Messages.Add($"Kein Datenordner {_dataPath} – nichts zu importieren.");
            return result;
        }

        // Je Profil die neueste Datei nehmen (umsaetze_*.json enthält die volle Historie).
        var dateien = Directory.GetFiles(_dataPath, "*historie*.json")
            .Concat(Directory.GetFiles(_dataPath, "umsaetze*.json"))
            .GroupBy(f => Path.GetFileName(f).ToLowerInvariant().Contains("paypal") ? "paypal" : "sparkasse")
            .Select(g => g.OrderByDescending(File.GetLastWriteTimeUtc).First())
            .ToList();

        if (dateien.Count == 0)
        {
            result.Messages.Add($"{_dataPath}: keine Umsatzdateien gefunden.");
            return result;
        }

        await using var db = await _factory.CreateDbContextAsync(ct);

        foreach (var datei in dateien)
        {
            try
            {
                await ImportDateiAsync(db, datei, result, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Bankimport fehlgeschlagen für {Datei}", datei);
                result.Messages.Add($"{Path.GetFileName(datei)}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync(ct);
        result.Transfers = await ErkenneUmbuchungenAsync(db, ct);
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Bankimport: {Summary}", result.Summary);
        return result;
    }

    private async Task ImportDateiAsync(AppDbContext db, string datei, BankImportResult result, CancellationToken ct)
    {
        using var stream = File.OpenRead(datei);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("accounts", out var konten))
            return;

        var (kontoname, kontotyp, farbe, icon) = ProfilFuer(datei);

        foreach (var konto in konten.EnumerateArray())
        {
            var iban = konto.TryGetProperty("iban", out var ib) && ib.ValueKind == JsonValueKind.String
                ? ib.GetString()
                : null;

            var account = await db.Accounts.FirstOrDefaultAsync(a =>
                (iban != null && a.Iban == iban) || a.Name == kontoname, ct);

            if (account == null)
            {
                account = new Account
                {
                    Name = kontoname,
                    AccountType = kontotyp,
                    ColorHex = farbe,
                    Icon = icon,
                    Iban = iban,
                    Currency = "EUR",
                };
                db.Accounts.Add(account);
                await db.SaveChangesAsync(ct);
                result.Accounts++;
            }
            else if (account.Iban == null && iban != null)
            {
                account.Iban = iban;
            }

            if (!konto.TryGetProperty("transactions", out var umsaetze))
                continue;

            var bekannt = await db.Transactions
                .Where(t => t.AccountId == account.Id && t.ExternalId != null)
                .Select(t => t.ExternalId!)
                .ToListAsync(ct);
            var bekanntSet = bekannt.ToHashSet(StringComparer.Ordinal);

            foreach (var eintrag in umsaetze.EnumerateArray())
            {
                var ext = eintrag.TryGetProperty("entry_reference", out var er) ? er.GetString() : null;
                if (string.IsNullOrWhiteSpace(ext) || bekanntSet.Contains(ext!))
                {
                    result.Skipped++;
                    continue;
                }

                var neu = MapEintrag(eintrag, account.Id, ext);
                if (neu == null)
                {
                    result.Skipped++;
                    continue;
                }

                db.Transactions.Add(neu);
                bekanntSet.Add(ext!);
                result.Inserted++;
            }

            await db.SaveChangesAsync(ct);
            await SetzeStartsaldoAsync(db, account, LiesKontostand(konto), ct);
        }
    }

    /// <summary>Verfügbarer bzw. gebuchter Kontostand aus der Bankantwort.</summary>
    private static decimal? LiesKontostand(JsonElement konto)
    {
        if (!konto.TryGetProperty("balances", out var salden) || salden.ValueKind != JsonValueKind.Array)
            return null;

        decimal? fallback = null;
        foreach (var saldo in salden.EnumerateArray())
        {
            if (!saldo.TryGetProperty("balance_amount", out var betrag) ||
                !betrag.TryGetProperty("amount", out var wert)) continue;

            if (!decimal.TryParse(wert.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var zahl))
                continue;

            var typ = saldo.TryGetProperty("balance_type", out var t) ? t.GetString() : null;
            if (string.Equals(typ, "ITAV", StringComparison.OrdinalIgnoreCase)) return zahl;
            if (string.Equals(typ, "CLBD", StringComparison.OrdinalIgnoreCase)) fallback = zahl;
        }
        return fallback;
    }

    private static Transaction? MapEintrag(JsonElement eintrag, int accountId, string? externalId)
    {
        if (!eintrag.TryGetProperty("transaction_amount", out var betrag) ||
            !betrag.TryGetProperty("amount", out var betragWert))
            return null;

        if (!decimal.TryParse(betragWert.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var summe))
            return null;

        var waehrung = betrag.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "EUR" : "EUR";
        var richtung = eintrag.TryGetProperty("credit_debit_indicator", out var cdi) ? cdi.GetString() : "DBIT";
        var istEingang = string.Equals(richtung, "CRDT", StringComparison.OrdinalIgnoreCase);

        var datum = LeseDatum(eintrag);
        var gegenpartei = LiesName(eintrag, istEingang ? "debtor" : "creditor");

        return new Transaction
        {
            AccountId = accountId,
            Amount = Math.Abs(summe),
            Type = istEingang ? TransactionType.Income : TransactionType.Expense,
            Date = datum,
            Note = LiesVerwendungszweck(eintrag),
            Merchant = Aufraeumen(gegenpartei),
            ExternalId = externalId,
            Status = eintrag.TryGetProperty("status", out var st) ? st.GetString() : "BOOK",
            Source = "enablebanking",
            OriginalCurrency = string.Equals(waehrung, "EUR", StringComparison.OrdinalIgnoreCase) ? null : waehrung,
        };
    }

    private static DateTime LeseDatum(JsonElement eintrag)
    {
        foreach (var feld in new[] { "booking_date", "value_date", "transaction_date" })
        {
            if (eintrag.TryGetProperty(feld, out var wert) && wert.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(wert.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.Date;
        }
        return DateTime.Today;
    }

    private static string? LiesName(JsonElement eintrag, string feld)
    {
        if (!eintrag.TryGetProperty(feld, out var partei) || partei.ValueKind != JsonValueKind.Object)
            return null;
        return partei.TryGetProperty("name", out var name) ? name.GetString() : null;
    }

    private static string? LiesVerwendungszweck(JsonElement eintrag)
    {
        var teile = new List<string>();
        if (eintrag.TryGetProperty("remittance_information", out var info))
        {
            if (info.ValueKind == JsonValueKind.Array)
                teile.AddRange(info.EnumerateArray().Select(t => t.GetString()).Where(t => !string.IsNullOrWhiteSpace(t))!);
            else if (info.ValueKind == JsonValueKind.String)
                teile.Add(info.GetString()!);
        }
        if (eintrag.TryGetProperty("note", out var notiz) && notiz.ValueKind == JsonValueKind.String)
            teile.Add(notiz.GetString()!);

        var text = string.Join(" · ", teile.Where(t => !string.IsNullOrWhiteSpace(t)));
        return string.IsNullOrWhiteSpace(text) ? null : (text.Length > 500 ? text[..500] : text);
    }

    /// <summary>„PAYPAL *NETFLIX.COM 40293" wird zu „Netflix.com".</summary>
    public static string? Aufraeumen(string? roh)
    {
        if (string.IsNullOrWhiteSpace(roh)) return null;
        var s = roh.Trim();

        s = Regex.Replace(s, @"^(PAYPAL\s*\*|PP\.?\*|SQ\s*\*|SP\s*\*|IZ\s*\*|CARD\s*\*)\s*", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+\d{4,}$", "");                 // Referenznummer am Ende
        s = Regex.Replace(s, @"\s{2,}", " ");
        s = s.Trim(' ', '-', '*', '.', ',');

        if (s.Length > 2 && s == s.ToUpperInvariant())
        {
            var klein = s.ToLowerInvariant();
            s = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(klein);
        }

        // „Netflix.Com" -> „Netflix.com"
        s = Regex.Replace(s, @"\.([A-Z]{2,4})$", m => "." + m.Groups[1].Value.ToLowerInvariant());
        return s.Length > 200 ? s[..200] : s;
    }

    /// <summary>
    /// Startsaldo so setzen, dass der berechnete Kontostand zum echten Kontostand passt.
    /// SQLite kann decimal nicht summieren, deshalb wird in Erinnerung gerechnet.
    /// </summary>
    private async Task SetzeStartsaldoAsync(AppDbContext db, Account account, decimal? echterStand, CancellationToken ct)
    {
        var zeilen = await db.Transactions
            .Where(t => t.AccountId == account.Id)
            .Select(t => new { t.Type, t.Amount })
            .ToListAsync(ct);

        var netto = zeilen.Sum(z => z.Type == TransactionType.Income ? z.Amount : -z.Amount);
        account.InitialBalance = echterStand.HasValue ? echterStand.Value - netto : account.InitialBalance;

        _log.LogInformation("Konto {Name}: {Anzahl} Buchungen, Netto {Netto}, Startsaldo {Saldo}",
            account.Name, zeilen.Count, netto, account.InitialBalance);
    }

    /// <summary>
    /// Paare finden, die zwischen zwei eigenen Konten verschoben wurden (gleicher Betrag,
    /// entgegengesetzte Richtung, wenige Tage Abstand) und als Umbuchung markieren.
    /// </summary>
    private async Task<int> ErkenneUmbuchungenAsync(AppDbContext db, CancellationToken ct)
    {
        var offen = await db.Transactions
            .Where(t => !t.IsTransfer && (t.Status == null || t.Status == "BOOK"))
            .OrderBy(t => t.Date)
            .ToListAsync(ct);

        var treffer = 0;
        var benutzt = new HashSet<int>();

        for (var i = 0; i < offen.Count; i++)
        {
            var a = offen[i];
            if (benutzt.Contains(a.Id)) continue;

            var partner = offen.FirstOrDefault(b =>
                b.Id != a.Id && !benutzt.Contains(b.Id) &&
                b.AccountId != a.AccountId &&
                b.Type != a.Type &&
                Math.Abs(b.Amount - a.Amount) < 0.01m &&
                Math.Abs((b.Date - a.Date).TotalDays) <= 3);

            if (partner == null) continue;

            a.IsTransfer = true;
            partner.IsTransfer = true;
            benutzt.Add(a.Id);
            benutzt.Add(partner.Id);
            treffer += 2;
        }

        return treffer;
    }
}
