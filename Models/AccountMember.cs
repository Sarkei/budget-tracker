using System;
using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Models;

public enum AccountRole
{
    Owner = 0,
    Viewer = 1
}

// Links a User to an Account with a role (owner or viewer)
public class AccountMember
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    [Required]
    public int UserId { get; set; }

    public AccountRole Role { get; set; } = AccountRole.Owner;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public User User { get; set; } = null!;
}
