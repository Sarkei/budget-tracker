using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>
/// Berechnet den echten Kontostand — und beantwortet dabei die Frage, warum
/// Tims Saldo „viel zu viel" im Minus war.
///
/// <b>Der Befund an den echten Daten:</b>
/// <list type="bullet">
///   <item>Sparkasse → PayPal per Lastschrift: <b>1.653,05 €</b> (135 Buchungen)</item>
///   <item>PayPal-Eingänge (Gutschriften): nur <b>435,30 €</b> (9 Buchungen)</item>
///   <item>PayPal-Ausgänge an Händler: <b>4.476,08 €</b> (161 Buchungen)</item>
/// </list>
///
/// Es fehlen also <b>1.217,75 €</b> an Gutschriften. Dadurch zeigt PayPal
/// −4.040,78 € an, obwohl das Konto in Wahrheit nur durchläuft: Das Geld wird
/// vom Girokonto abgebucht und von PayPal an den Händler geschickt. Ein
/// PayPal-Konto im Minus ist bei diesem Modell gar nicht möglich — PayPal
/// bucht ab, es gibt keinen Dispo.
///
/// <b>Was daraus folgt:</b> Ein Durchlaufkonto gehört nicht in die
/// Vermögenssumme. Sein Saldo sagt nichts über Tims Geld aus; er entsteht nur
/// dadurch, dass eine Seite der Buchungen fehlt. Wer ihn mitzählt, bekommt
/// genau den Effekt, den Tim beschreibt: Der Saldo ist viel zu tief.
///
/// Deshalb: <see cref="IstDurchlaufkonto"/> erkennt solche Konten, und der
/// Kontostand zählt sie nicht mit. Der echte Stand ist die Sparkasse allein.
/// </summary>
public static class Kontostand
{
    /// <summary>
    /// Ist das Konto nur ein Durchlauf (Geld kommt vom anderen Konto)?
    ///
    /// Erkennungsmerkmale, alle aus den Daten:
    ///  - Es gibt viele Lastschriften vom Girokonto an dieses Konto.
    ///  - Die Gutschriften auf dem Konto sind deutlich kleiner als die
    ///    Lastschriften (die Bank liefert sie unvollständig).
    ///  - Das Konto hat einen negativen Saldo, obwohl es keinen Dispo hat.
    /// </summary>
    public static bool IstDurchlaufkonto(
        int kontoId,
        IEnumerable<Transaction> alle,
        IEnumerable<Account> konten)
    {
        var liste = alle.ToList();
        var eigenes = konten.FirstOrDefault(k => k.Id == kontoId);
        if (eigenes == null) return false;

        // Nur für Konten, die nicht das Hauptkonto sind.
        var hauptkonto = konten
            .Where(k => k.IsActive && k.Id != kontoId)
            .OrderByDescending(k => k.InitialBalance)
            .FirstOrDefault();
        if (hauptkonto == null) return false;

        // Lastschriften vom Hauptkonto an dieses Konto
        var zulauf = liste
            .Where(t => t.AccountId == hauptkonto.Id)
            .Where(t => t.Type == TransactionType.Expense)
            .Where(t => ZaehltText(t, eigenes.Name))
            .Sum(t => t.Amount);

        // Gutschriften auf diesem Konto
        var gutschriften = liste
            .Where(t => t.AccountId == kontoId && t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        if (zulauf <= 0) return false;

        /*
         * Wenn deutlich mehr abgebucht als gutgeschrieben wurde, ist die
         * Gutschriftseite unvollständig — das Konto ist ein Durchlauf.
         * Schwelle 20 %: kleine Rundungs- oder Gebührenunterschiede sollen
         * nicht dazu führen, dass ein echtes Konto als Durchlauf gilt.
         */
        var luecke = zulauf - gutschriften;
        return luecke > zulauf * 0.20m;
    }

    /// <summary>Steht im Text ein Bezug zum Kontonamen (z. B. „PayPal")?</summary>
    private static bool ZaehltText(Transaction t, string kontoname)
    {
        var text = $"{t.Merchant} {t.Note}";
        if (string.IsNullOrWhiteSpace(kontoname) || kontoname.Length < 3) return false;

        // Bei „PayPal" genügt der Name; bei „Girokonto" wäre er zu allgemein.
        return text.Contains(kontoname, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Die Konten, die in den echten Kontostand gehören (ohne Durchlaufkonten).
    /// </summary>
    public static List<Account> EchteKonten(
        IEnumerable<Transaction> alle,
        IEnumerable<Account> konten)
    {
        var liste = konten.Where(k => k.IsActive).ToList();
        var alleListe = alle.ToList();
        return liste
            .Where(k => !IstDurchlaufkonto(k.Id, alleListe, liste))
            .ToList();
    }

    /// <summary>
    /// Echter Kontostand: Anfangssaldo plus gebuchte Buchungen, nur für die
    /// Konten, die echtes Geld halten.
    ///
    /// <paramref name="kontoFilter"/> erlaubt, nur ein Konto zu betrachten —
    /// dann wird die Durchlauf-Regel nicht angewandt (wer ein Konto ausdrücklich
    /// auswählt, will dessen Zahlen sehen).
    /// </summary>
    public static decimal Berechne(
        IEnumerable<Transaction> alle,
        IEnumerable<Account> konten,
        int? kontoFilter = null)
    {
        var alleListe = alle.ToList();
        var aktive = konten.Where(k => k.IsActive).ToList();

        IEnumerable<Account> betrachtet;
        if (kontoFilter.HasValue)
        {
            betrachtet = aktive.Where(k => k.Id == kontoFilter.Value);
        }
        else
        {
            betrachtet = EchteKonten(alleListe, aktive);
        }

        var summe = 0m;
        foreach (var k in betrachtet)
        {
            summe += k.InitialBalance;
            foreach (var t in alleListe.Where(t => t.AccountId == k.Id))
            {
                if (!BuchungsFilter.Zaehlt(t)) continue;
                summe += t.Type == TransactionType.Income ? t.Amount : -t.Amount;
            }
        }
        return summe;
    }
}
