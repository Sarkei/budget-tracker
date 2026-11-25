using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

/// <summary>
/// Wissensstand zu einem Abo: wo man kündigt und wie.
///
/// Tims Wunsch: „ich will das du im hintergrund quasi raussuchst wo man ne email
/// hinschreiben muss damit ich da kündigen kann". Dieses Modell hält das
/// Ergebnis dieser Suche fest — pro Anbieter einmal, nicht bei jedem Aufruf neu.
///
/// <b>Wichtig:</b> Eine E-Mail-Adresse allein genügt bei den meisten Anbietern
/// nicht. Spotify und Plex verlangen die Kündigung im eigenen Konto; eine Mail
/// ist dort nur der zweite Weg. Deshalb steht in <see cref="Kuendigungsweg"/>
/// beides: der direkte Link und, falls vorhanden, die Adresse für eine Mail.
/// Wer nur die Mail schickt und nie im Konto kündigt, zahlt weiter.
/// </summary>
public class AboAnbieter
{
    [Key]
    public int Id { get; set; }

    /// <summary>Schlüssel zum Zuordnen (klein, ohne Firmenzusätze) — z. B. "spotify".</summary>
    [Required]
    [MaxLength(120)]
    public string Schluessel { get; set; } = string.Empty;

    /// <summary>Anzeigename des Anbieters.</summary>
    [Required]
    [MaxLength(200)]
    public string Anzeigename { get; set; } = string.Empty;

    /// <summary>Adresse für die Kündigung per E-Mail, falls der Anbieter das anbietet.</summary>
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>Direkter Link zur Kündigung im Konto (der sicherste Weg).</summary>
    [MaxLength(300)]
    public string? KuendigungsLink { get; set; }

    /// <summary>Kurzer Text, wie die Kündigung abläuft.</summary>
    [MaxLength(500)]
    public string? Kuendigungsweg { get; set; }

    /// <summary>
    /// Ist das ein Abo, das per E-Mail gekündigt werden kann?
    ///
    /// Bei Ratenkäufen (Amazon-Ratenzahlung, OTTO Payments) ist das bewusst
    /// false: Eine Rate läuft bis zum Ende, die kündigt man nicht per Mail —
    /// man tilgt sie. Tims ausdrücklicher Hinweis.
    /// </summary>
    public bool PerEmailKuendbar { get; set; }

    /// <summary>Ist der Eintrag geprüft (von Tim bestätigt) oder nur recherchiert?</summary>
    public bool Geprueft { get; set; }

    /// <summary>Wann zuletzt recherchiert.</summary>
    public DateTime Stand { get; set; } = DateTime.UtcNow;
}

/// <summary>Zustand einer Kündigung.</summary>
public enum KuendigungsStatus
{
    /// <summary>Noch nichts unternommen.</summary>
    Offen = 0,
    /// <summary>Mail ist vorbereitet, wartet auf Tims Freigabe.</summary>
    Vorbereitet = 1,
    /// <summary>Freigegeben und in die Warteschlange gelegt.</summary>
    Versendet = 2,
    /// <summary>Anbieter hat bestätigt.</summary>
    Bestaetigt = 3,
    /// <summary>Fehlgeschlagen, mit Grund.</summary>
    Fehlgeschlagen = 4,
    /// <summary>Zurückgezogen — Tim will doch behalten.</summary>
    Verworfen = 5,
}

/// <summary>
/// Eine Kündigung zu einem Abo.
///
/// Der Ablauf ist bewusst dreistufig, damit nichts ohne Tims Wissen rausgeht:
///  1. <b>Vorbereiten</b> — die App baut den Text aus seinen Daten und zeigt ihn.
///  2. <b>Freigeben</b> — Tim liest und bestätigt (oder ändert den Text).
///  3. <b>Versenden</b> — erst jetzt landet die Mail in der Warteschlange.
///
/// Ein automatischer Versand im Hintergrund wäre bequemer, ist aber zu riskant:
/// ein falscher Empfänger kündigt das falsche Abo, und bei Ratenkäufen wäre die
/// Mail sogar schädlich.
/// </summary>
public class Kuendigung
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string AboName { get; set; } = string.Empty;

    /// <summary>Empfänger der Kündigung.</summary>
    [MaxLength(200)]
    public string? Empfaenger { get; set; }

    [MaxLength(300)]
    public string? Betreff { get; set; }

    /// <summary>Der Text, so wie er rausgeht.</summary>
    public string? Text { get; set; }

    public KuendigungsStatus Status { get; set; } = KuendigungsStatus.Offen;

    /// <summary>Monatlicher Betrag, der wegfällt — für die Anzeige des Nutzens.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BetragProMonat { get; set; }

    /// <summary>Wann vorbereitet.</summary>
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

    /// <summary>Wann freigegeben und verschickt.</summary>
    public DateTime? VersendetAm { get; set; }

    /// <summary>Grund bei Fehlschlag oder Hinweis.</summary>
    [MaxLength(500)]
    public string? Hinweis { get; set; }

    /// <summary>Nutzer, der die Kündigung angelegt hat.</summary>
    public int UserId { get; set; }
}
