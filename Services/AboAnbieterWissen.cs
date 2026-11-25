using BudgetTracker.Models;

namespace BudgetTracker.Services;

/// <summary>
/// Wo man welche Abos kündigt — die Wissensbasis der App.
///
/// Tims Wunsch war, dass die App 'im hintergrund raussucht, wo man eine E-Mail
/// hinschreiben muss'. Zwei Dinge sind dabei wichtig, und beide stehen hier
/// ausdrücklich drin:
///
///  1. <b>Eine Mail ist oft nicht der richtige Weg.</b> Spotify, Plex und viele
///     andere verlangen die Kündigung im eigenen Konto. Wer nur schreibt, zahlt
///     weiter. Deshalb hat jeder Eintrag den direkten Kündigungslink — der ist
///     der sichere Weg, die Mail nur die Absicherung.
///
///  2. <b>Ratenkäufe sind keine Abos.</b> Eine Amazon-Ratenzahlung oder eine
///     OTTO-Rate kann man nicht kündigen; sie läuft bis zum Ende und wird
///     getilgt. Diese Anbieter sind als `PerEmailKuendbar = false` markiert und
///     die App bietet für sie gar keinen Kündigungs-Knopf an.
///
/// Die Einträge sind Startwissen. Was die App nicht kennt, zeigt sie als
/// 'unbekannt' an und schlägt die Suche im Konto vor, statt eine Adresse zu
/// erfinden.
/// </summary>
public static class AboAnbieterWissen
{
    /// <summary>Bekannte Anbieter. Schlüssel ist der normalisierte Name.</summary>
    public static List<AboAnbieter> Standard() => new()
    {
        new AboAnbieter
        {
            Schluessel = "spotify",
            Anzeigename = "Spotify",
            Email = "kundendienst_de@spotify.com",
            KuendigungsLink = "https://www.spotify.com/de/account/subscription/",
            Kuendigungsweg = "Im Konto unter Abo auf Abo kündigen. Eine Mail an den "
                           + "Kundendienst geht zusätzlich, ist aber nicht der offizielle Weg.",
            PerEmailKuendbar = true,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "plex",
            Anzeigename = "Plex",
            Email = "support@plex.tv",
            KuendigungsLink = "https://account.plex.tv/de/subscriptions",
            Kuendigungsweg = "Im Plex-Konto unter Abonnements & Zahlungen kündigen. "
                           + "Bei Kauf über Google Play dort kündigen, nicht bei Plex.",
            PerEmailKuendbar = true,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "netflix",
            Anzeigename = "Netflix",
            KuendigungsLink = "https://www.netflix.com/cancelplan",
            Kuendigungsweg = "Nur im Konto kündbar, keine Kündigung per E-Mail möglich.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "disney",
            Anzeigename = "Disney+",
            KuendigungsLink = "https://www.disneyplus.com/account",
            Kuendigungsweg = "Nur im Konto unter Abonnement kündbar.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "newshosting",
            Anzeigename = "Newshosting",
            Email = "billing@newshosting.com",
            KuendigungsLink = "https://www.newshosting.com/",
            Kuendigungsweg = "Per E-Mail an die Buchhaltung oder im Kundenkonto. "
                           + "Kündigungsfrist beachten — verlängert sich sonst automatisch.",
            PerEmailKuendbar = true,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "swappie",
            Anzeigename = "Swappie",
            Email = "support@swappie.com",
            KuendigungsLink = "https://swappie.com/de/",
            Kuendigungsweg = "Händler mit Ratenkauf. Eine Rate ist kein Abo — "
                           + "Restzahlung tilgen oder Support anschreiben.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "klarna",
            Anzeigename = "Klarna",
            KuendigungsLink = "https://app.klarna.com/",
            Kuendigungsweg = "Klarna ist eine Zahlungsart, kein Abo. Die Raten laufen beim "
                           + "Händler — dort kündigen oder tilgen.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "hanseatic",
            Anzeigename = "Hanseatic Bank",
            Email = "service@hanseaticbank.de",
            KuendigungsLink = "https://www.hanseaticbank.de/",
            Kuendigungsweg = "Kredit- oder Ratenvertrag — kein Abo im Wortsinn. "
                           + "Vertrag schriftlich kündigen oder Restzahlung leisten.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "amazon",
            Anzeigename = "Amazon",
            KuendigungsLink = "https://www.amazon.de/gp/primecentral",
            Kuendigungsweg = "Abos (Prime, Channels) im Konto unter Mitgliedschaften. "
                           + "Ratenzahlungen sind keine Abos und nicht kündbar.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "otto",
            Anzeigename = "OTTO Payments",
            KuendigungsLink = "https://www.otto.de/",
            Kuendigungsweg = "Ratenzahlung — kein Abo. Restbetrag tilgen oder den "
                           + "Ratenplan im OTTO-Konto anpassen.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "db vertrieb",
            Anzeigename = "DB Vertrieb",
            KuendigungsLink = "https://www.bahn.de/service/buchung/abos",
            Kuendigungsweg = "BahnCard und Zeitkarten: im Bahn-Konto oder schriftlich "
                           + "kündigen. Frist meist 6 Wochen zum Laufzeitende.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "google",
            Anzeigename = "Google",
            KuendigungsLink = "https://play.google.com/store/account/subscriptions",
            Kuendigungsweg = "Alle Google-Abos zentral unter Abos im Play Store kündbar.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "microsoft",
            Anzeigename = "Microsoft",
            KuendigungsLink = "https://account.microsoft.com/services",
            Kuendigungsweg = "Im Microsoft-Konto unter Dienste & Abos kündbar.",
            PerEmailKuendbar = false,
            Geprueft = false,
        },
        new AboAnbieter
        {
            Schluessel = "freenet",
            Anzeigename = "freenet",
            Email = "service@freenet.de",
            KuendigungsLink = "https://www.freenet.de/mein-freenet/",
            Kuendigungsweg = "Im Kundenkonto kündigen; Mail an den Service geht zusätzlich.",
            PerEmailKuendbar = true,
            Geprueft = false,
        },
    };

    /// <summary>
    /// Sucht den passenden Anbieter zu einem Namen aus den Buchungen.
    ///
    /// Bewusst unscharf: Banken schreiben 'Spotify AB', 'SPOTIFY AB' oder
    /// 'Spotify Stockholm' — der Kernname bleibt gleich.
    /// </summary>
    public static AboAnbieter? Finde(IEnumerable<AboAnbieter> anbieter, string name)
    {
        var n = Normalisiere(name);
        if (n.Length == 0) return null;

        AboAnbieter? beste = null;
        var besteLaenge = 0;

        foreach (var a in anbieter)
        {
            var schluessel = Normalisiere(a.Schluessel);
            var anzeige = Normalisiere(a.Anzeigename);
            foreach (var k in new[] { schluessel, anzeige })
            {
                if (k.Length < 3) continue;
                if (!n.Contains(k, StringComparison.Ordinal)) continue;
                /* Der längste Treffer gewinnt: 'PayPal Europe' soll nicht
                   'PayPal' schlagen, wenn 'PayPal Europe' ein eigener Eintrag ist. */
                if (k.Length > besteLaenge)
                {
                    beste = a;
                    besteLaenge = k.Length;
                }
            }
        }
        return beste;
    }

    /// <summary>Kleinschreibung, ohne Satzzeichen und Firmenzusätze.</summary>
    public static string Normalisiere(string s)
    {
        var t = (s ?? "").ToLowerInvariant();
        foreach (var weg in new[] { " se", " ab", " gmbh", " co kg", " inc", " llc",
                                    " s.a r.l.", " s.a.r.l.", " europe", " deutschland",
                                    " payments", " eu s.a r.l.", " niederlassung",
                                    ",", ".", "-" })
            t = t.Replace(weg, " ");
        return string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
