using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>
/// Eine Stelle für alles, was „zählt" und was nicht.
///
/// Warum das nötig war: Jede Seite hat ihre Summen selbst gebaut und dabei
/// alle Zeilen mitgezählt. Im Bestand lagen dadurch 26 Buchungen in der
/// Rechnung, die nicht im Kontostand stehen — vorgemerkte (PDNG), stornierte
/// (CNCL) und abgelehnte (RJCT). Ergebnis: Der angezeigte Saldo lag 38,10 €
/// unter dem echten Stand, und zwar auf jeder Seite anders.
///
/// Regeln (aus dem Datenmodell, nicht erfunden):
///  - Nur gebuchte Zeilen zählen. `BOOK` ist gebucht, `PDNG` vorgemerkt;
///    `CNCL`/`RJCT` sind storniert bzw. abgelehnt. Fehlt der Status (manuell
///    erfasste Buchung), gilt sie als gebucht.
///  - Umbuchungen zwischen eigenen Konten (`IsTransfer`) verändern das
///    Vermögen nicht und zählen nicht in Einnahmen/Ausgaben.
/// </summary>
public static class BuchungsFilter
{
    /// <summary>Zählt diese Buchung in Summen und Salden?</summary>
    public static bool Zaehlt(Transaction t)
    {
        if (t.IsTransfer) return false;
        // Fehlender Status = manuell erfasst = gebucht.
        if (string.IsNullOrWhiteSpace(t.Status)) return true;
        return string.Equals(t.Status, "BOOK", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nur die Zeilen, die in Summen gehören.</summary>
    public static IEnumerable<Transaction> Relevante(IEnumerable<Transaction> quelle)
        => quelle.Where(Zaehlt);

    /// <summary>Queryable-Variante, damit die Datenbank filtert statt der Server.</summary>
    public static IQueryable<Transaction> Relevante(this IQueryable<Transaction> q)
        => q.Where(t => !t.IsTransfer
                        && (t.Status == null || t.Status == "" || t.Status == "BOOK"));

    /// <summary>Wird die Buchung noch erwartet (vorgemerkt)?</summary>
    public static bool IstVorgemerkt(Transaction t)
        => !t.IsTransfer
           && string.Equals(t.Status, "PDNG", StringComparison.OrdinalIgnoreCase);

    /// <summary>Wurde die Buchung storniert oder abgelehnt?</summary>
    public static bool IstVerworfen(Transaction t)
        => !t.IsTransfer
           && (string.Equals(t.Status, "CNCL", StringComparison.OrdinalIgnoreCase)
               || string.Equals(t.Status, "RJCT", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Wie <see cref="Saldo(decimal, IEnumerable{Transaction})"/>, berücksichtigt
    /// aber doppelt verbuchte PayPal-Zahlungen.
    ///
    /// Tims Beobachtung: „bei paypal zum beispiel wird alles als saldo angezeigt
    /// auch wenn es ratenkäufe sind das ist bisschen problematisch." An den
    /// Daten nachgemessen: 1.165,04 € stehen sowohl auf dem Girokonto als auch
    /// auf dem PayPal-Konto als Ausgabe — dasselbe Geld, zweimal gebucht.
    /// </summary>
    /// <param name="startwert">Anfangssaldo des Kontos.</param>
    /// <param name="buchungen">Alle Buchungen des Kontos.</param>
    /// <param name="doppelt">Die IDs der Buchungen, die nicht zählen (Doppelungen).</param>
    public static decimal Saldo(decimal startwert, IEnumerable<Transaction> buchungen, ISet<int>? doppelt)
    {
        if (doppelt == null || doppelt.Count == 0) return Saldo(startwert, buchungen);
        var summe = 0m;
        foreach (var t in buchungen)
        {
            if (!Zaehlt(t) || doppelt.Contains(t.Id)) continue;
            summe += t.Type == TransactionType.Income ? t.Amount : -t.Amount;
        }
        return startwert + summe;
    }

    /// <summary>
    /// Saldo eines Kontos: Startwert plus gebuchte Einnahmen minus gebuchte
    /// Ausgaben. Vorgemerkte und verworfene Zeilen bleiben außen vor.
    /// </summary>
    public static decimal Saldo(decimal startwert, IEnumerable<Transaction> buchungen)
    {
        var summe = 0m;
        foreach (var t in buchungen)
        {
            if (!Zaehlt(t)) continue;
            summe += t.Type == TransactionType.Income ? t.Amount : -t.Amount;
        }
        return startwert + summe;
    }
}
