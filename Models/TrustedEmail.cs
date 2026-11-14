using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Models;

public class TrustedEmail
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public int AddedByUserId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
