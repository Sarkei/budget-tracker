using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<RecurringItem> RecurringItems { get; set; }
    public DbSet<ScheduledPayment> ScheduledPayments { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AccountMember> AccountMembers { get; set; }
    public DbSet<SavingsPot> SavingsPots { get; set; }
    public DbSet<SharedGroup> SharedGroups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<GroupExpense> GroupExpenses { get; set; }
    public DbSet<ExpenseSplit> ExpenseSplits { get; set; }
    public DbSet<GroupInvitation> GroupInvitations { get; set; }
    public DbSet<GroupSettlement> GroupSettlements { get; set; }
    public DbSet<UserPreferences> UserPreferences { get; set; }
    public DbSet<TrustedEmail> TrustedEmails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account relationships
        modelBuilder.Entity<Account>()
            .HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.RecurringItems)
            .WithOne(r => r.Account)
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.ScheduledPayments)
            .WithOne(s => s.Account)
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.Members)
            .WithOne(m => m.Account)
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Category relationships (set null on delete to preserve history)
        modelBuilder.Entity<Category>()
            .HasMany(c => c.Transactions)
            .WithOne(t => t.Category)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.RecurringItems)
            .WithOne(r => r.Category)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.ScheduledPayments)
            .WithOne(s => s.Category)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // RecurringItem -> Transaction (generated transactions)
        modelBuilder.Entity<RecurringItem>()
            .HasMany(r => r.GeneratedTransactions)
            .WithOne(t => t.RecurringItem)
            .HasForeignKey(t => t.RecurringItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // ScheduledPayment -> Transaction
        modelBuilder.Entity<ScheduledPayment>()
            .HasMany<Transaction>()
            .WithOne(t => t.ScheduledPayment)
            .HasForeignKey(t => t.ScheduledPaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        // User unique username
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // TrustedEmail unique email
        modelBuilder.Entity<TrustedEmail>()
            .HasIndex(t => t.Email)
            .IsUnique();

        // AccountMember: each user can only appear once per account
        modelBuilder.Entity<AccountMember>()
            .HasIndex(m => new { m.AccountId, m.UserId })
            .IsUnique();

        modelBuilder.Entity<AccountMember>()
            .HasOne(m => m.Account)
            .WithMany(a => a.Members)
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountMember>()
            .HasOne(m => m.User)
            .WithMany(u => u.AccountMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // SharedGroup
        modelBuilder.Entity<SharedGroup>()
            .HasIndex(g => g.Slug)
            .IsUnique();

        modelBuilder.Entity<SharedGroup>()
            .HasOne(g => g.CreatedByUser)
            .WithMany()
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SharedGroup>()
            .HasMany(g => g.Members)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SharedGroup>()
            .HasMany(g => g.Expenses)
            .WithOne(e => e.Group)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // GroupMember: each user can only appear once per group
        modelBuilder.Entity<GroupMember>()
            .HasIndex(m => new { m.GroupId, m.UserId })
            .IsUnique();

        modelBuilder.Entity<GroupMember>()
            .HasOne(m => m.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // GroupExpense
        modelBuilder.Entity<GroupExpense>()
            .HasOne(e => e.PaidByUser)
            .WithMany()
            .HasForeignKey(e => e.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupExpense>()
            .HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GroupExpense>()
            .HasMany(e => e.Splits)
            .WithOne(s => s.GroupExpense)
            .HasForeignKey(s => s.GroupExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // ExpenseSplit
        modelBuilder.Entity<ExpenseSplit>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // GroupInvitation
        modelBuilder.Entity<GroupInvitation>()
            .HasOne(i => i.Group)
            .WithMany()
            .HasForeignKey(i => i.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupInvitation>()
            .HasOne(i => i.InvitedByUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // GroupSettlement
        modelBuilder.Entity<GroupSettlement>()
            .HasOne(s => s.Group)
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupSettlement>()
            .HasOne(s => s.Payer)
            .WithMany()
            .HasForeignKey(s => s.PayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupSettlement>()
            .HasOne(s => s.Payee)
            .WithMany()
            .HasForeignKey(s => s.PayeeUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
