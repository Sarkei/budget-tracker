using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public class ScheduledPayment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    public int? CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Amount in account base currency
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Original amount if paid in foreign currency
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalAmount { get; set; }

    [MaxLength(10)]
    public string? OriginalCurrency { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    // The date when this payment is due
    public DateTime DueDate { get; set; }

    public bool IsPaid { get; set; } = false;

    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
}
