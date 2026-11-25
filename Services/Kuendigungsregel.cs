using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>
/// Die Regeln, ob ein Abo kündbar ist — ohne Datenbank, damit sie prüfbar sind.
///
/// Tims Warnung war ausdrücklich: „aufpassen das ratenkäufe keine abos sind die
/// man kündigen kann". Deshalb steht diese Entscheidung an einer Stelle und
/// ist für jeden Posten nachvollziehbar begründet.
///
/// Grundsatz: Ein Abo ist eine laufende Vereinbarung, die man beenden kann.
/// Eine Rate ist eine Schuld, die man tilgt — kündigen ist dort der falsche
/// Begriff und würde nur Zeit kosten.
/// </summary>
public static class Kuendigungsregel
{
    /// <summary>Ist der Posten überhaupt kündbar?</summary>
    public static (bool Kuendbar, string Grund) Pruefe(AboPosten abo, AboAnbieter? anbieter)
    {
        /* Beide Quellen prüfen: Name UND Verwendungszweck. Bei Amazon steht der
           Ratenhinweis nur im Verwendungszweck — ohne diese Prüfung böte die App
           für eine laufende Ratenzahlung einen Kündigungs-Knopf an. */
        if (IstRatenkauf(abo.Name, abo.Verwendungszweck))
            return (false, "Ratenzahlung — läuft bis zur letzten Rate, nicht kündbar.");

        if (anbieter == null)
            return (true, "Anbieter unbekannt — Kündigung im Kundenkonto prüfen.");

        if (!anbieter.PerEmailKuendbar && string.IsNullOrWhiteSpace(anbieter.Email))
        {
            var weg = string.IsNullOrWhiteSpace(anbieter.Kuendigungsweg)
                ? "Nur im Kundenkonto kündbar."
                : anbieter.Kuendigungsweg;
            return (false, weg);
        }

        return (true, anbieter.Kuendigungsweg ?? "Kündigung möglich.");
    }

    /// <summary>
    /// Erkennt Ratenkäufe — am Namen oder an der Buchung selbst.
    ///
    /// Der reine Name genügt nicht: In Tims Daten heißt die Amazon-Rate
    /// „Amazon Eu S.A R.L., Niederlassung Deutschland", der Hinweis
    /// „AMZN-Ratenzahlung" steht nur im Verwendungszweck. Wer nur den Namen
    /// prüft, bietet für eine laufende Ratenzahlung einen Kündigungs-Knopf an —
    /// genau das, was Tim ausgeschlossen haben wollte.
    ///
    /// Deshalb wird hier auch der Verwendungszweck geprüft.
    /// </summary>
    public static bool IstRatenkauf(string name)
    {
        var n = (name ?? "").ToLowerInvariant();
        return n.Contains("rate")
               || n.Contains("otto payments")
               || n.Contains("klarna")
               || n.Contains("hanseatic")
               || n.Contains("swappie")
               || n.Contains("teilzahl");
    }

    /// <summary>
    /// Ratenkauf-Erkennung mit Verwendungszweck.
    ///
    /// Wird für die Anzeige gebraucht: Der Anzeigename („Amazon") verschweigt,
    /// dass es eine Rate ist. Der Hinweis steht in der Buchung.
    /// </summary>
    public static bool IstRatenkauf(string name, string? verwendungszweck)
    {
        if (IstRatenkauf(name)) return true;
        var v = (verwendungszweck ?? "").ToLowerInvariant();
        return v.Contains("ratenzahlung")
               || v.Contains("raten-zahlung")
               || v.Contains("amzn-raten")
               || v.Contains("teilzahlung")
               || v.Contains("rechnung r-de-");
    }


    /// <summary>Hat der Anbieter einen Weg, den Tim selbst gehen kann?</summary>
    public static bool HatDirektLink(AboAnbieter? anbieter)
        => anbieter != null && !string.IsNullOrWhiteSpace(anbieter.KuendigungsLink);

    /// <summary>Kann per E-Mail gekündigt werden?</summary>
    public static bool PerEmail(AboAnbieter? anbieter)
        => anbieter != null
           && anbieter.PerEmailKuendbar
           && !string.IsNullOrWhiteSpace(anbieter.Email);
}
