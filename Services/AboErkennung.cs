using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>Ein erkanntes oder gepflegtes Abo.</summary>
public class AboPosten
{
    public string Name { get; set; } = "";
    /// <summary>Betrag je Zahlung; negativ = Ausgabe.</summary>
    public decimal Betrag { get; set; }
    public TransactionType Typ { get; set; } = TransactionType.Expense;
    public string? Kategorie { get; set; }
    /// <summary>Monatlich, Wöchentlich, Jährlich …</summary>
    public string Rhythmus { get; set; } = "monatlich";
    /// <summary>Betrag auf einen Monat umgerechnet — für Summen.</summary>
    public decimal ProMonat { get; set; }
    /// <summary>Letzte beobachtete Zahlung.</summary>
    public DateTime LetzteZahlung { get; set; }
    /// <summary>Nächster erwarteter Termin.</summary>
    public DateTime NaechsteZahlung { get; set; }
    /// <summary>Wie oft in den Daten gesehen.</summary>
    public int Anzahl { get; set; }
    /// <summary>Woher der Eintrag stammt: erkannt oder gepflegt.</summary>
    public string Quelle { get; set; } = "erkannt";
    /// <summary>0..1 — wie sicher die Erkennung ist.</summary>
    public double Sicherheit { get; set; }
    public string? Konto { get; set; }
    /// <summary>
    /// Verwendungszweck einer Beispielbuchung. Wird gebraucht, um Ratenkäufe zu
    /// erkennen: Bei Amazon steht „AMZN-Ratenzahlung" nur hier, nicht im Namen.
    /// </summary>
    public string? Verwendungszweck { get; set; }
}

/// <summary>
/// Erkennt Abos in den gebuchten Zahlungen.
///
/// Tims Wunsch: „ich will das da automatisch die abos und so angezeigt werden".
/// Es gibt bereits gepflegte Wiederkehrende Posten — die haben Vorrang. Was
/// dort fehlt, wird hier aus der Historie abgeleitet: gleicher Empfänger,
/// ähnlicher Betrag, wiederkehrender Abstand.
///
/// Bewusst heuristisch und nachvollziehbar: lieber ein Abo als „wahrscheinlich"
/// markiert als eine Zahl, die niemand erklären kann. Die Sicherheit steht
/// deshalb in jedem Eintrag.
/// </summary>
public static class AboErkennung
{
    /// <summary>Ab welcher Anzahl Zahlungen gilt etwas als wiederkehrend?</summary>
    private const int MinZahlungen = 3;

    /// <summary>Wie stark darf der Betrag abweichen, damit es noch dasselbe Abo ist?</summary>
    private const double BetragsToleranz = 0.25;

    /// <summary>
    /// Findet Abos in den übergebenen Buchungen (bereits gefiltert auf gebucht).
    /// Gepflegte Posten werden bevorzugt und nicht doppelt gemeldet.
    /// </summary>
    public static List<AboPosten> Finde(
        IEnumerable<Transaction> buchungen,
        IEnumerable<RecurringItem> gepflegt,
        Func<int?, string?>? kontoName = null)
    {
        var ergebnis = new List<AboPosten>();
        var schonAbgedeckt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Gepflegte Posten zuerst — die sind bewusst angelegt und exakt.
        foreach (var r in gepflegt.Where(r => r.IsActive))
        {
            var proMonat = AufMonat(r.Amount, r.Interval);
            ergebnis.Add(new AboPosten
            {
                Name = r.Name,
                Betrag = r.Type == TransactionType.Expense ? -Math.Abs(r.Amount) : Math.Abs(r.Amount),
                Typ = r.Type,
                Rhythmus = RhythmusText(r.Interval),
                ProMonat = proMonat,
                LetzteZahlung = r.StartDate,
                NaechsteZahlung = NaechsterTermin(r),
                Anzahl = 0,
                Quelle = "gepflegt",
                Sicherheit = 1.0,
                Konto = kontoName?.Invoke(r.AccountId),
            });
            schonAbgedeckt.Add(Normalisiert(r.Name));
        }

        // 2) Aus der Historie erkennen.
        var verdaechtig = buchungen
            .Where(t => !t.IsTransfer)
            .Where(t => !string.IsNullOrWhiteSpace(t.Merchant) || !string.IsNullOrWhiteSpace(t.Note))
            .GroupBy(t => Normalisiert(t.Merchant ?? t.Note ?? ""))
            .Where(g => g.Count() >= MinZahlungen);

        foreach (var gruppe in verdaechtig)
        {
            if (schonAbgedeckt.Contains(gruppe.Key)) continue;

            var liste = gruppe.OrderBy(t => t.Date).ToList();
            var betraege = liste.Select(t => Math.Abs(t.Amount)).ToList();
            var schnitt = betraege.Average();
            if (schnitt <= 0) continue;

            // Alle Beträge müssen nah beieinander liegen, sonst ist es kein Abo
            // (der Supermarkt um die Ecke hat auch viele Zahlungen).
            var passend = betraege.Count(b => (double)(Math.Abs(b - schnitt) / schnitt) <= BetragsToleranz);
            if (passend < MinZahlungen || (double)passend / betraege.Count < 0.7) continue;

            // Abstand zwischen den Zahlungen prüfen.
            var tage = liste.Zip(liste.Skip(1), (a, b) => (b.Date - a.Date).TotalDays).ToList();
            if (tage.Count < 2) continue;
            var mittlererAbstand = tage.Average();
            var rhythmus = RhythmusAusAbstand(mittlererAbstand);
            if (rhythmus == null) continue;

            // Auch der Abstand muss einigermaßen regelmäßig sein.
            var abweichung = tage.Average(d => Math.Abs(d - mittlererAbstand));
            var regelmaessig = abweichung / mittlererAbstand <= 0.5;
            if (!regelmaessig) continue;

            var letzte = liste[^1];
            var istEinnahme = liste.Count(t => t.Type == TransactionType.Income) > liste.Count / 2;
            var betrag = istEinnahme ? Math.Abs(schnitt) : -Math.Abs(schnitt);

            // Sicherheit aus Anzahl und Regelmäßigkeit.
            var sicherheit = Math.Min(1.0,
                0.5
                + Math.Min(0.3, (liste.Count - MinZahlungen) * 0.1)
                + (regelmaessig ? 0.2 : 0));

            ergebnis.Add(new AboPosten
            {
                Name = SchönName(letzte.Merchant ?? letzte.Note ?? gruppe.Key),
                Betrag = Math.Round(betrag, 2),
                Typ = istEinnahme ? TransactionType.Income : TransactionType.Expense,
                Kategorie = null,
                Rhythmus = rhythmus.Value.Text,
                ProMonat = AufMonat(Math.Abs(betrag), rhythmus.Value.Interval),
                LetzteZahlung = letzte.Date,
                NaechsteZahlung = letzte.Date.AddDays(mittlererAbstand),
                Anzahl = liste.Count,
                Quelle = "erkannt",
                Sicherheit = Math.Round(sicherheit, 2),
                Konto = kontoName?.Invoke(letzte.AccountId),
                Verwendungszweck = letzte.Note,
            });
        }

        return ergebnis
            .OrderByDescending(a => Math.Abs(a.ProMonat))
            .ToList();
    }

    /// <summary>Text für die Anzeige.</summary>
    public static string RhythmusText(RecurringInterval i) => i switch
    {
        RecurringInterval.Weekly => "wöchentlich",
        RecurringInterval.Monthly => "monatlich",
        RecurringInterval.Quarterly => "vierteljährlich",
        RecurringInterval.Yearly => "jährlich",
        _ => "monatlich",
    };

    /// <summary>Betrag auf einen Monat umrechnen (für Summen).</summary>
    public static decimal AufMonat(decimal betrag, RecurringInterval i) => i switch
    {
        RecurringInterval.Weekly => Math.Round(betrag * 52m / 12m, 2),
        RecurringInterval.Monthly => Math.Round(betrag, 2),
        RecurringInterval.Quarterly => Math.Round(betrag / 3m, 2),
        RecurringInterval.Yearly => Math.Round(betrag / 12m, 2),
        _ => Math.Round(betrag, 2),
    };

    /// <summary>Wann ist der Posten das nächste Mal fällig?</summary>
    public static DateTime NaechsterTermin(RecurringItem r)
    {
        var heute = DateTime.Today;
        if (r.EndDate.HasValue && r.EndDate.Value < heute) return r.EndDate.Value;

        // Bei Ratenzahlung zählt, wie viele noch offen sind.
        if (r.IsInstallment && r.TotalInstallments.HasValue && r.PaidInstallments >= r.TotalInstallments.Value)
            return r.EndDate ?? heute;

        var termin = r.StartDate.Date;
        if (termin > heute) return termin;

        switch (r.Interval)
        {
            case RecurringInterval.Weekly:
                while (termin < heute) termin = termin.AddDays(7);
                break;
            case RecurringInterval.Monthly:
                while (termin < heute) termin = termin.AddMonths(1);
                break;
            case RecurringInterval.Quarterly:
                while (termin < heute) termin = termin.AddMonths(3);
                break;
            case RecurringInterval.Yearly:
                while (termin < heute) termin = termin.AddYears(1);
                break;
        }
        return termin;
    }

    /// <summary>Rhythmus aus dem mittleren Abstand in Tagen ableiten.</summary>
    private static (string Text, RecurringInterval Interval)? RhythmusAusAbstand(double tage)
    {
        if (tage >= 5 && tage <= 9) return ("wöchentlich", RecurringInterval.Weekly);
        if (tage >= 12 && tage <= 18) return ("zweiwöchentlich", RecurringInterval.Weekly);
        if (tage >= 25 && tage <= 36) return ("monatlich", RecurringInterval.Monthly);
        if (tage >= 80 && tage <= 100) return ("vierteljährlich", RecurringInterval.Quarterly);
        if (tage >= 350 && tage <= 380) return ("jährlich", RecurringInterval.Yearly);
        return null;
    }

    /// <summary>Schlüssel zum Vergleichen von Empfängern.</summary>
    private static string Normalisiert(string s)
    {
        var t = (s ?? "").ToLowerInvariant();
        // Bankzusätze und Nummern entfernen, damit „AMAZON EU S.A R.L." und
        // „Amazon" zusammenfinden.
        foreach (var weg in new[] { " se", " s.a r.l.", " s.a.r.l.", " eu ", " gmbh", " co kg",
                                    " niederlassung", " de", " hong kong", " paris", " dublin",
                                    " moorestown", " hamburg", " verkaufshaus" })
        {
            t = t.Replace(weg, " ");
        }
        t = new string(t.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray());
        var teile = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", teile.Take(2));
    }

    /// <summary>Anzeigenamen aufhübschen.</summary>
    private static string SchönName(string roh)
    {
        var t = (roh ?? "").Trim();
        if (t.Length == 0) return "Unbekannt";
        if (t == t.ToUpperInvariant())
        {
            // „AMAZON EU S.A R.L." -> „Amazon Eu"
            t = string.Join(" ", t.Split(' ').Take(2));
            return char.ToUpperInvariant(t[0]) + t[1..].ToLowerInvariant();
        }
        return t;
    }
}
