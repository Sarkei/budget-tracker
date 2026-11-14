using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public enum RecurringInterval
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}

public class RecurringItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    public int? CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // ISO 4217 currency code
    [MaxLength(10)]
    public string Currency { get; set; } = "EUR";

    public TransactionType Type { get; set; }

    public RecurringInterval Interval { get; set; } = RecurringInterval.Monthly;

    // Day of month for monthly interval (1-31)
    public int DayOfMonth { get; set; } = 1;

    public DateTime StartDate { get; set; } = DateTime.Today;

    // Null = indefinite (like rent, salary). Set for installments.
    public DateTime? EndDate { get; set; }

    // True if this is an installment payment (Ratenzahlung) with a fixed number of payments
    public bool IsInstallment { get; set; } = false;

    // Total number of installments (only relevant if IsInstallment = true)
    public int? TotalInstallments { get; set; }

    // How many installments have been paid so far
    public int PaidInstallments { get; set; } = 0;

    [MaxLength(500)]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
    public ICollection<Transaction> GeneratedTransactions { get; set; } = new List<Transaction>();
}
