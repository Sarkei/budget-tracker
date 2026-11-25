using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>
/// Erkennt Umbuchungen zwischen den eigenen Konten.
///
/// Tims Beobachtung: „bei paypal zum beispiel wird alles als saldo angezeigt
/// auch wenn es ratenkäufe sind das ist bisschen problematisch."
///
/// Zwei getrennte Probleme stecken dahinter:
///
///  1. <b>Aufladung doppelt gezählt.</b> Wenn Tim per Lastschrift 200 € auf
///     PayPal schiebt, steht die Ausgabe auf dem Girokonto — der Zugang auf
///     PayPal wird von der Bank aber oft nicht als eigene Buchung geliefert.
///     Dann fehlen 200 € und der PayPal-Saldo ist dauerhaft zu niedrig.
///     (Im Bestand: 1.653,05 € abgebucht, nur 435,30 € gutgeschrieben.)
///
///  2. <b>Ratenkäufe.</b> Eine Ratenzahlung ist keine Umbuchung — sie ist eine
///     echte Ausgabe, nur in Teilen. Sie darf nicht in den Saldo der
///     Gesamtsumme des Kaufs eingehen, sondern nur mit der Rate, die wirklich
///     abgebucht wurde. Die Raten stehen einzeln in den Daten; sie werden
///     erkannt und als Abo geführt, damit der Ausblick sie einplant.
///
/// Der Abgleich läuft über Betrag und Datum, nicht über Freitext — Banken
/// schreiben den Verwendungszweck jedes Mal anders.
/// </summary>
public static class TransferErkennung
{
    /// <summary>Wie viele Tage dürfen zwischen Abbuchung und Gutschrift liegen?</summary>
    private const int MaxTageAbstand = 4;

    /// <summary>Betragstoleranz in Euro (Rundung, Gebühren).</summary>
    private const decimal Toleranz = 0.05m;

    /// <summary>Ein erkanntes oder fehlendes Gegenstück.</summary>
    public class Befund
    {
        /// <summary>Die Buchung auf dem abbuchenden Konto.</summary>
        public Transaction Ausgang { get; set; } = null!;
        /// <summary>Das Gegenstück, falls vorhanden.</summary>
        public Transaction? Eingang { get; set; }
        /// <summary>Fehlt die Gutschrift auf dem Zielkonto?</summary>
        public bool FehltGutschrift { get; set; }
    }

    /// <summary>
    /// Sucht Aufladungen, deren Gutschrift fehlt.
    ///
    /// Bewusst vorsichtig: Es wird nur gemeldet, was eindeutig ist. Bei
    /// Händlerzahlungen über PayPal existiert keine Gutschrift — die dürfen
    /// nicht als fehlend gelten, sonst würde der Saldo künstlich erhöht.
    /// </summary>
    public static List<Befund> FindeFehlendeGutschriften(
        IEnumerable<Transaction> buchungen,
        Func<int, bool> istPaypalKonto)
    {
        var alle = buchungen.Where(t => !t.IsTransfer).ToList();

        var ausgaenge = alle
            .Where(t => t.Type == TransactionType.Expense)
            .Where(t => !istPaypalKonto(t.AccountId))
            .Where(t => IstPaypalBezug(t))
            .ToList();

        var eingaenge = alle
            .Where(t => t.Type == TransactionType.Income)
            .Where(t => istPaypalKonto(t.AccountId))
            .ToList();

        var ergebnis = new List<Befund>();
        var verbraucht = new HashSet<int>();

        foreach (var a in ausgaenge.OrderBy(t => t.Date))
        {
            // Gibt es eine Gutschrift auf PayPal mit gleichem Betrag in der Nähe?
            var partner = eingaenge.FirstOrDefault(e =>
                !verbraucht.Contains(e.Id)
                && Math.Abs(e.Amount - a.Amount) <= Toleranz
                && Math.Abs((e.Date.Date - a.Date.Date).TotalDays) <= MaxTageAbstand);

            if (partner != null)
            {
                verbraucht.Add(partner.Id);
                ergebnis.Add(new Befund { Ausgang = a, Eingang = partner, FehltGutschrift = false });
            }
            else
            {
                /*
                 * Keine Gutschrift gefunden. Ob das ein Fehler ist, hängt davon
                 * ab, ob es eine Aufladung war oder eine Händlerzahlung, die
                 * PayPal nur vermittelt hat. Eine Händlerzahlung erkennt man
                 * daran, dass auf der PayPal-Seite eine Ausgabe gleichen
                 * Betrags steht.
                 */
                var haendler = alle.Any(p =>
                    istPaypalKonto(p.AccountId)
                    && p.Type == TransactionType.Expense
                    && Math.Abs(p.Amount - a.Amount) <= Toleranz
                    && Math.Abs((p.Date.Date - a.Date.Date).TotalDays) <= MaxTageAbstand * 2);

                if (!haendler)
                {
                    ergebnis.Add(new Befund { Ausgang = a, Eingang = null, FehltGutschrift = true });
                }
            }
        }

        return ergebnis;
    }

    /// <summary>
    /// Findet Bankabbuchungen, deren Betrag auf dem Zielkonto ein zweites Mal
    /// als Zahlung auftaucht.
    ///
    /// Tims Beobachtung, an echten Zahlen belegt: Von der Sparkasse gingen
    /// 1.653,05 € an PayPal, auf der PayPal-Seite stehen 161 Ausgaben über
    /// 4.476,08 €. Bei rund 1.086 € ist derselbe Betrag innerhalb von zwei
    /// Tagen auf beiden Konten als Ausgabe verbucht.
    ///
    /// Was dabei wirklich passiert: Das Geld verlässt das Girokonto (echt), wird
    /// PayPal gutgeschrieben (fehlt in den Daten) und geht von PayPal an den
    /// Händler (echt). Weil die Gutschrift fehlt, sieht es aus, als hätte PayPal
    /// doppelt bezahlt.
    ///
    /// Deshalb wird hier das <b>PayPal-Gegenstück</b> als Doppelung markiert,
    /// nicht die Bankabbuchung: Das Girokonto hat das Geld wirklich verloren,
    /// PayPal hat es nie bekommen. Wer die Bankseite herausrechnet, macht das
    /// Girokonto künstlich reicher — genau das war im ersten Versuch zu sehen
    /// (Konto 2 sprang von 173,75 € auf 1.232,11 €).
    /// </summary>
    public static List<Doppelzahlung> FindeDoppelzahlungen(
        IEnumerable<Transaction> buchungen,
        Func<int, bool> istPaypalKonto)
    {
        var alle = buchungen.Where(t => !t.IsTransfer).ToList();

        var bank = alle
            .Where(t => !istPaypalKonto(t.AccountId))
            .Where(t => t.Type == TransactionType.Expense)
            .Where(IstPaypalBezug)
            .ToList();

        var paypal = alle
            .Where(t => istPaypalKonto(t.AccountId))
            .Where(t => t.Type == TransactionType.Expense)
            .ToList();

        var ergebnis = new List<Doppelzahlung>();
        var schonGenutzt = new HashSet<int>();

        foreach (var a in bank.OrderBy(t => t.Date))
        {
            var partner = paypal.FirstOrDefault(p =>
                !schonGenutzt.Contains(p.Id)
                && Math.Abs(p.Amount - a.Amount) <= Toleranz
                && Math.Abs((p.Date.Date - a.Date.Date).TotalDays) <= 2);

            if (partner == null) continue;
            schonGenutzt.Add(partner.Id);
            ergebnis.Add(new Doppelzahlung { Bank = a, Paypal = partner });
        }

        return ergebnis;
    }

    /// <summary>Eine doppelt verbuchte Zahlung.</summary>
    public class Doppelzahlung
    {
        public Transaction Bank { get; set; } = null!;
        public Transaction Paypal { get; set; } = null!;
    }

    /// <summary>Steht in der Buchung ein PayPal-Bezug?</summary>
    public static bool IstPaypalBezug(Transaction t)
    {
        var text = $"{t.Merchant} {t.Note}".ToLowerInvariant();
        return text.Contains("paypal");
    }

    /// <summary>
    /// Erkennt Ratenkäufe: derselbe Händler, ähnlicher Betrag, monatlicher
    /// Abstand, und in der Buchung steht ein Hinweis auf Raten.
    ///
    /// Wird für die Anzeige gebraucht — ein Ratenkauf ist eine laufende
    /// Verpflichtung, kein einmaliger Kauf.
    /// </summary>
    public static List<Ratenkauf> FindeRatenkaeufe(IEnumerable<Transaction> buchungen)
    {
        var liste = buchungen
            .Where(t => !t.IsTransfer && t.Type == TransactionType.Expense)
            .ToList();

        var kandidaten = liste
            .Where(IstRatenHinweis)
            .GroupBy(t => Normalisiert(t.Merchant ?? t.Note ?? ""))
            .Where(g => g.Count() >= 2);

        var ergebnis = new List<Ratenkauf>();

        foreach (var g in kandidaten)
        {
            var posten = g.OrderBy(t => t.Date).ToList();
            var betraege = posten.Select(t => Math.Abs(t.Amount)).ToList();
            var schnitt = betraege.Average();
            // Raten sind fast immer gleich hoch.
            if (betraege.Any(b => (double)(Math.Abs(b - schnitt) / schnitt) > 0.15)) continue;

            var abstaende = posten.Zip(posten.Skip(1), (a, b) => (b.Date - a.Date).TotalDays).ToList();
            if (abstaende.Count == 0) continue;
            // Monatlicher Rhythmus?
            var mittel = abstaende.Average();
            if (mittel < 25 || mittel > 36) continue;

            ergebnis.Add(new Ratenkauf
            {
                Name = posten[^1].Merchant ?? g.Key,
                Rate = Math.Round(schnitt, 2),
                BisherBezahlt = posten.Count,
                SummeBisher = Math.Round(betraege.Sum(), 2),
                LetzteRate = posten[^1].Date,
                NaechsteRate = posten[^1].Date.AddDays((int)Math.Round(mittel)),
                RhythmusTage = (int)Math.Round(mittel),
                Konto = posten[^1].AccountId,
            });
        }

        return ergebnis.OrderByDescending(r => r.Rate).ToList();
    }

    /// <summary>Steht in der Buchung ein Hinweis auf eine Ratenzahlung?</summary>
    public static bool IstRatenHinweis(Transaction t)
    {
        var text = $"{t.Merchant} {t.Note}".ToLowerInvariant();
        return text.Contains("raten")
               || text.Contains("teilzahl")
               || text.Contains("später zahlen")
               || text.Contains("spaeter zahlen")
               || text.Contains("otto payments");
    }

    private static string Normalisiert(string s)
    {
        var t = (s ?? "").ToLowerInvariant();
        foreach (var weg in new[] { " se", " s.a r.l.", " s.a.r.l.", " eu ", " gmbh", " co kg",
                                    " niederlassung", " payments", " verkaufshaus" })
            t = t.Replace(weg, " ");
        t = new string(t.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray());
        return string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2));
    }
}

/// <summary>Ein erkannter Ratenkauf.</summary>
public class Ratenkauf
{
    public string Name { get; set; } = "";
    public decimal Rate { get; set; }
    public int BisherBezahlt { get; set; }
    public decimal SummeBisher { get; set; }
    public DateTime LetzteRate { get; set; }
    public DateTime NaechsteRate { get; set; }
    public int RhythmusTage { get; set; } = 30;
    public int Konto { get; set; }
}
