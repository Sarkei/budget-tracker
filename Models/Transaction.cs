using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public enum TransactionType
{
    Income = 0,
    Expense = 1
}

public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    public int? CategoryId { get; set; }

    // Amount stored always in the account's base currency (EUR by default)
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Original amount before conversion (if foreign currency was used)
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalAmount { get; set; }

    // Original currency code if different from account currency
    [MaxLength(10)]
    public string? OriginalCurrency { get; set; }

    // Exchange rate used for conversion (OriginalAmount * ExchangeRate = Amount)
    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    public TransactionType Type { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Link to scheduled payment if this was auto-generated from one
    public int? ScheduledPaymentId { get; set; }

    // Link to recurring item if this was auto-generated from one
    public int? RecurringItemId { get; set; }

    // Navigation
    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
    public ScheduledPayment? ScheduledPayment { get; set; }
    public RecurringItem? RecurringItem { get; set; }
}
