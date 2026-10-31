using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public class Account
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Hex color for visual distinction in sidebar and charts, e.g. "#42A5F5"
    [MaxLength(20)]
    public string ColorHex { get; set; } = "#42A5F5";

    // MaterialDesign icon name e.g. "Bank", "CreditCard", "Cash"
    [MaxLength(100)]
    public string Icon { get; set; } = "Bank";

    // Starting balance when the account was created in the app
    [Column(TypeName = "decimal(18,2)")]
    public decimal InitialBalance { get; set; } = 0m;

    // ISO 4217 currency code, e.g. "EUR"
    [MaxLength(10)]
    public string Currency { get; set; } = "EUR";

    // Account type label, e.g. "Girokonto", "Sparkonto"
    [MaxLength(50)]
    public string AccountType { get; set; } = "Girokonto";

    /// <summary>IBAN fuer die Zuordnung beim Bankabgleich.</summary>
    [MaxLength(34)]
    public string? Iban { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // If true, this account is selected by default when the user logs in
    public bool IsDefault { get; set; } = false;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringItem> RecurringItems { get; set; } = new List<RecurringItem>();
    public ICollection<ScheduledPayment> ScheduledPayments { get; set; } = new List<ScheduledPayment>();
    public ICollection<AccountMember> Members { get; set; } = new List<AccountMember>();
}
