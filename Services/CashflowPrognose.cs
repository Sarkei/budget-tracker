using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>Ein Tag im Ausblick.</summary>
public class PrognoseTag
{
    public DateTime Datum { get; set; }
    /// <summary>Was an diesem Tag von Abos und geplanten Zahlungen erwartet wird.</summary>
    public decimal Erwartet { get; set; }
    /// <summary>Kontostand am Ende des Tages.</summary>
    public decimal Stand { get; set; }
    /// <summary>Die Posten dieses Tages, für die Aufschlüsselung.</summary>
    public List<PrognosePosten> Posten { get; set; } = new();
    public bool IstHeute { get; set; }
}

/// <summary>Ein einzelner erwarteter Posten.</summary>
public class PrognosePosten
{
    public string Name { get; set; } = "";
    public decimal Betrag { get; set; }
    public string Art { get; set; } = "abo";
    public string? Konto { get; set; }
}

/// <summary>Ergebnis des Ausblicks.</summary>
public class Prognose
{
    public decimal StartSaldo { get; set; }
    public decimal EndSaldo { get; set; }
    public List<PrognoseTag> Tage { get; set; } = new();
    /// <summary>Summe aller erwarteten Abgänge im Zeitraum.</summary>
    public decimal Ausgaben { get; set; }
    /// <summary>Summe aller erwarteten Eingänge im Zeitraum.</summary>
    public decimal Einnahmen { get; set; }
    /// <summary>Der erste Tag, an dem der Stand unter null fällt.</summary>
    public DateTime? ErsterEngpass { get; set; }
    /// <summary>Durchschnittlicher Tagesverbrauch der letzten Wochen (nicht Abos).</summary>
    public decimal AlltagProTag { get; set; }
}

/// <summary>
/// Ausblick auf den Kontostand, wie bei Finanzguru.
///
/// Tims Wunsch: „das man sieht laut abos und vergangenen zahlungen und
/// einnahmen wie das konto aussehen kann bis zu einem monat im voraus."
///
/// Zwei Quellen fließen ein:
///  1. Bekannte Termine: Abos (erkannt und gepflegt) und geplante Zahlungen.
///     Die sind datumsgenau und werden an ihren Fälligkeitstag gesetzt.
///  2. Alltag: Was in den letzten Wochen zusätzlich ausgegeben wurde, also
///     alles, was nicht Abo ist (Einkäufe, Tanken, Bargeld). Das ist nicht
///     datumsgenau, wird deshalb gleichmäßig auf die Tage verteilt.
///
/// Bewusst so getrennt: Ein Abo kennt seinen Termin, ein Einkauf nicht. Beides
/// in einen Topf zu werfen ergäbe eine Zahl, die weder stimmt noch erklärt
/// werden kann.
/// </summary>
public static class CashflowPrognose
{
    /// <summary>Wie viele Tage in die Zukunft gerechnet wird.</summary>
    public const int StandardTage = 30;

    /// <summary>Für den Alltagsverbrauch betrachtete Vergangenheit in Tagen.</summary>
    private const int RueckblickTage = 60;

    /// <summary>
    /// Rechnet den Ausblick.
    /// </summary>
    /// <param name="startSaldo">Kontostand heute (nur gebuchte Buchungen).</param>
    /// <param name="abos">Erkannte und gepflegte Abos.</param>
    /// <param name="geplanteZahlungen">Geplante Zahlungen mit Fälligkeitsdatum.</param>
    /// <param name="vergangeneBuchungen">Gebuchte Buchungen der letzten Wochen (ohne Transfers).</param>
    /// <param name="aboNamen">Namen, die bereits als Abo zählen — nicht doppelt als Alltag.</param>
    /// <param name="heute">Bezugstag (für Tests überschreibbar).</param>
    public static Prognose Berechne(
        decimal startSaldo,
        IEnumerable<AboPosten> abos,
        IEnumerable<ScheduledPayment> geplanteZahlungen,
        IEnumerable<Transaction> vergangeneBuchungen,
        ISet<string>? aboNamen = null,
        DateTime? heute = null,
        int tage = StandardTage)
    {
        var start = (heute ?? DateTime.Today).Date;
        var ende = start.AddDays(tage);
        var ergebnis = new Prognose { StartSaldo = startSaldo };

        // Posten je Tag einsammeln.
        var proTag = new Dictionary<DateTime, List<PrognosePosten>>();
        void Merken(DateTime datum, PrognosePosten p)
        {
            var d = datum.Date;
            if (d < start || d > ende) return;
            if (!proTag.TryGetValue(d, out var liste)) proTag[d] = liste = new List<PrognosePosten>();
            liste.Add(p);
        }

        // ---- 1) Abos an ihren Terminen ----
        foreach (var a in abos)
        {
            if (a.ProMonat == 0) continue;
            // Ausgehend vom nächsten Termin alle künftigen im Zeitraum setzen.
            var termin = a.NaechsteZahlung.Date;
            var schritt = SchrittFuer(a.Rhythmus);
            // Falls der Termin in der Vergangenheit liegt, nach vorn schieben.
            var sicherung = 0;
            while (termin < start && sicherung++ < 500) termin = termin.AddDays(schritt);
            while (termin <= ende && sicherung++ < 500)
            {
                if (termin >= start)
                {
                    Merken(termin, new PrognosePosten
                    {
                        Name = a.Name,
                        Betrag = a.Betrag,
                        Art = "abo",
                        Konto = a.Konto,
                    });
                }
                termin = termin.AddDays(schritt);
            }
        }

        // ---- 2) Geplante Zahlungen ----
        foreach (var s in geplanteZahlungen.Where(s => !s.IsPaid))
        {
            Merken(s.DueDate, new PrognosePosten
            {
                Name = string.IsNullOrWhiteSpace(s.Name) ? "Geplante Zahlung" : s.Name,
                Betrag = s.Type == TransactionType.Income ? Math.Abs(s.Amount) : -Math.Abs(s.Amount),
                Art = "geplant",
            });
        }

        // ---- 3) Alltag aus der Vergangenheit ----
        var grenze = start.AddDays(-RueckblickTage);
        var namen = aboNamen ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alltag = vergangeneBuchungen
            .Where(t => !t.IsTransfer)
            .Where(t => t.Date >= grenze && t.Date < start)
            .Where(t => t.Type == TransactionType.Expense)
            // Was ein Abo ist, wird oben schon terminlich gerechnet.
            .Where(t => !IstAboName(t, namen))
            .ToList();

        var alltagProTag = alltag.Count > 0
            ? Math.Round(alltag.Sum(t => t.Amount) / RueckblickTage, 2)
            : 0m;
        ergebnis.AlltagProTag = alltagProTag;

        // Gleichmäßig verteilen — ab morgen, damit „heute" den echten Stand zeigt.
        for (var d = start.AddDays(1); d <= ende; d = d.AddDays(1))
        {
            if (alltagProTag > 0)
            {
                Merken(d, new PrognosePosten
                {
                    Name = "Alltag (Schnitt)",
                    Betrag = -alltagProTag,
                    Art = "alltag",
                });
            }
        }

        // ---- 4) Tagessummen und Verlauf ----
        var stand = startSaldo;
        for (var d = start; d <= ende; d = d.AddDays(1))
        {
            var posten = proTag.TryGetValue(d, out var l) ? l : new List<PrognosePosten>();
            var summe = posten.Sum(p => p.Betrag);
            stand += summe;

            foreach (var p in posten)
            {
                if (p.Betrag >= 0) ergebnis.Einnahmen += p.Betrag;
                else ergebnis.Ausgaben += -p.Betrag;
            }

            ergebnis.Tage.Add(new PrognoseTag
            {
                Datum = d,
                Erwartet = Math.Round(summe, 2),
                Stand = Math.Round(stand, 2),
                Posten = posten.OrderBy(p => p.Betrag).ToList(),
                IstHeute = d == start,
            });

            if (ergebnis.ErsterEngpass == null && stand < 0 && d >= start)
                ergebnis.ErsterEngpass = d;
        }

        ergebnis.EndSaldo = Math.Round(stand, 2);
        ergebnis.Ausgaben = Math.Round(ergebnis.Ausgaben, 2);
        ergebnis.Einnahmen = Math.Round(ergebnis.Einnahmen, 2);
        return ergebnis;
    }

    /// <summary>Abstand in Tagen für einen Rhythmus-Text.</summary>
    private static int SchrittFuer(string rhythmus) => rhythmus?.ToLowerInvariant() switch
    {
        "wöchentlich" => 7,
        "zweiwöchentlich" => 14,
        "vierteljährlich" => 91,
        "jährlich" => 365,
        _ => 30,
    };

    /// <summary>Gehört dieser Empfänger zu einem bekannten Abo?</summary>
    private static bool IstAboName(Transaction t, ISet<string> namen)
    {
        if (namen.Count == 0) return false;
        var text = $"{t.Merchant} {t.Note}".ToLowerInvariant();
        foreach (var n in namen)
        {
            if (n.Length >= 3 && text.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
