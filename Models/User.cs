using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Models;

public enum UserRole
{
    Admin = 0,
    Member = 1
}

public class User
{
    [Key]
    public int Id { get; set; }

    // Email is used as the login identifier (stored as Username for backwards compat)
    [Required]
    [MaxLength(200)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    // BCrypt hash
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.Member;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public ICollection<AccountMember> AccountMemberships { get; set; } = new List<AccountMember>();
    public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
}
