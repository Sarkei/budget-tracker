using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Models;

public class UserPreferences
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    // ── Feature toggles ──────────────────────────────
    public bool ShowSavingsPots { get; set; } = true;
    public bool ShowRecurring { get; set; } = true;
    public bool ShowScheduledPayments { get; set; } = true;
    public bool ShowGroups { get; set; } = true;

    // ── Dashboard display ────────────────────────────
    public bool ShowAccountMiniBoxes { get; set; } = true;
    public bool CompactMode { get; set; } = false;

    // ── Formatting ───────────────────────────────────
    public string Currency { get; set; } = "EUR";
    public string DateFormat { get; set; } = "dd.MM.yyyy";
    public string Language { get; set; } = "de";

    // ── Budget warnings ──────────────────────────────
    public bool EnableBudgetWarnings { get; set; } = false;
    public decimal MonthlyBudgetLimit { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
