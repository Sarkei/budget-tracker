using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Models;

public enum CategoryType
{
    Income = 0,
    Expense = 1,
    Both = 2
}

public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // MaterialDesign icon name
    [MaxLength(100)]
    public string Icon { get; set; } = "Tag";

    // Hex color used in charts and badges
    [MaxLength(20)]
    public string ColorHex { get; set; } = "#9E9E9E";

    public CategoryType Type { get; set; } = CategoryType.Both;

    public bool IsDefault { get; set; } = false;

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringItem> RecurringItems { get; set; } = new List<RecurringItem>();
    public ICollection<ScheduledPayment> ScheduledPayments { get; set; } = new List<ScheduledPayment>();
}
