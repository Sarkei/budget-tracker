using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public class SavingsPot
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }

    // Saved amount (manually tracked, not linked to transactions in this version)
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAmount { get; set; }

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#42A5F5";

    [MaxLength(50)]
    public string Icon { get; set; } = "PiggyBank";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Percent progress (0-100)
    [NotMapped]
    public decimal ProgressPercent => TargetAmount > 0 ? Math.Min(100, CurrentAmount / TargetAmount * 100) : 0;
}
