using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetTracker.Models;

public enum GroupMemberRole
{
    Admin = 0,
    Member = 1
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

// A shared expense group (like a Splitwise group)
public class SharedGroup
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier, e.g. "meine-gruppe". Unique, set on creation.</summary>
    [MaxLength(150)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#42A5F5";

    public int CreatedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? CreatedByUser { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<GroupExpense> Expenses { get; set; } = new List<GroupExpense>();
}

// A user participating in a shared expense group
public class GroupMember
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GroupId { get; set; }

    [Required]
    public int UserId { get; set; }

    public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SharedGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}

// A single expense within a group
public class GroupExpense
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GroupId { get; set; }

    // Who paid this expense
    [Required]
    public int PaidByUserId { get; set; }

    public int? CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "EUR";

    public DateTime Date { get; set; } = DateTime.Today;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Recurring expense support
    public bool IsRecurring { get; set; } = false;
    public RecurringInterval? RecurringInterval { get; set; }

    // Income tracking (positive split for all members instead of expense)
    public bool IsIncome { get; set; } = false;

    // History-only import (excluded from current debt calculations)
    public bool IsImported { get; set; } = false;

    // Navigation
    public SharedGroup Group { get; set; } = null!;
    public User PaidByUser { get; set; } = null!;
    public Category? Category { get; set; }
    public ICollection<ExpenseSplit> Splits { get; set; } = new List<ExpenseSplit>();
}

// How each member's share of a group expense is split
public class ExpenseSplit
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GroupExpenseId { get; set; }

    // Which user owes this share
    [Required]
    public int UserId { get; set; }

    // Amount this user owes
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Whether this split has been settled (paid back) - kept for backward compat, balance uses GroupSettlements
    public bool IsSettled { get; set; } = false;

    public DateTime? SettledAt { get; set; }

    // Navigation
    public GroupExpense GroupExpense { get; set; } = null!;
    public User User { get; set; } = null!;
}

// An invitation to join a shared group (sent by email)
public class GroupInvitation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GroupId { get; set; }

    [Required]
    public int InvitedByUserId { get; set; }

    // Email address the invitation was sent to
    [Required]
    [MaxLength(200)]
    public string InviteeEmail { get; set; } = string.Empty;

    // Resolved once the invitation is accepted
    public int? InviteeUserId { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    // Navigation
    public SharedGroup Group { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}

// Records a payment between group members (full or partial debt settlement)
public class GroupSettlement
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GroupId { get; set; }

    // Who is paying
    [Required]
    public int PayerUserId { get; set; }

    // Who is receiving the payment
    [Required]
    public int PayeeUserId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SharedGroup Group { get; set; } = null!;
    public User Payer { get; set; } = null!;
    public User Payee { get; set; } = null!;
}
