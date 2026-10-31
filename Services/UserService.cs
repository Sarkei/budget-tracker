using BudgetTracker.Data;
using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

public enum RegisterOutcome { Success, AlreadyExists, NotTrusted }
public enum LoginOutcome { Success, InvalidCredentials, AccountDisabled }

public class UserService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public UserService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// Die Adresse aus OWNER_EMAIL ist der Besitzer der Installation: immer Admin und
    /// die einzige Person, die die Finanzdaten sehen darf.
    /// </summary>
    public static string BesitzerEmail =>
        (Environment.GetEnvironmentVariable("OWNER_EMAIL") ?? "nagraszust@gmail.com").Trim().ToLowerInvariant();

    public static bool IstBesitzer(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        string.Equals(email.Trim().ToLowerInvariant(), BesitzerEmail, StringComparison.Ordinal);

    public async Task<bool> IsFirstRunAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return !await db.Users.AnyAsync();
    }

    public async Task<(RegisterOutcome Outcome, User? User)> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Username == normalized))
            return (RegisterOutcome.AlreadyExists, null);

        bool isFirst = !await db.Users.AnyAsync();

        if (!isFirst && !await db.TrustedEmails.AnyAsync(t => t.Email == normalized))
            return (RegisterOutcome.NotTrusted, null);

        var user = new User
        {
            Username = normalized,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = $"{firstName.Trim()} {lastName.Trim()}",
            Role = (isFirst || IstBesitzer(email)) ? UserRole.Admin : UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (RegisterOutcome.Success, user);
    }

    public async Task<(LoginOutcome Outcome, User? User)> ValidateLoginAsync(string email, string password)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == normalized);

        if (user == null) return (LoginOutcome.InvalidCredentials, null);
        if (!user.IsActive) return (LoginOutcome.AccountDisabled, null);
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return (LoginOutcome.InvalidCredentials, null);

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (LoginOutcome.Success, user);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
    }

    public async Task<List<User>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users.Where(u => u.IsActive).OrderBy(u => u.DisplayName).ToListAsync();
    }

    public async Task<List<User>> GetAllUsersAdminAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.Role).ThenBy(u => u.DisplayName).ToListAsync();
    }

    public static int? GetUserId(System.Security.Claims.ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    public async Task<bool> UpdateEmailAsync(int userId, string newEmail)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = newEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Username == normalized && u.Id != userId))
            return false;
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;
        user.Username = normalized;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsEmailTrustedAsync(string email)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = email.Trim().ToLowerInvariant();
        return await db.TrustedEmails.AnyAsync(t => t.Email == normalized);
    }

    /// <summary>Admin-only: update email + optionally add to trusted list. Returns false if email already taken by another user.</summary>
    public async Task<bool> UpdateEmailAdminAsync(int userId, string newEmail)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = newEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Username == normalized && u.Id != userId))
            return false;
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;
        user.Username = normalized;
        // Keep pending invitations in sync — update InviteeEmail so the user still sees them after re-login
        var pendingInvites = await db.GroupInvitations
            .Where(i => i.InviteeUserId == userId && i.Status == InvitationStatus.Pending)
            .ToListAsync();
        foreach (var inv in pendingInvites)
            inv.InviteeEmail = normalized;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNameAsync(int userId, string firstName, string lastName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;
        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.DisplayName = $"{firstName.Trim()} {lastName.Trim()}".Trim();
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetUserActiveAsync(int userId, bool active)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsActive = active;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserPermanentlyAsync(int userId, int requestingAdminId)
    {
        if (userId == requestingAdminId) return false;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null || user.IsActive) return false;
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TrustedEmail>> GetTrustedEmailsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TrustedEmails.OrderByDescending(t => t.AddedAt).ToListAsync();
    }

    public async Task<bool> AddTrustedEmailAsync(string email, int addedByUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = email.Trim().ToLowerInvariant();
        if (await db.TrustedEmails.AnyAsync(t => t.Email == normalized)) return false;
        db.TrustedEmails.Add(new TrustedEmail { Email = normalized, AddedByUserId = addedByUserId, AddedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveTrustedEmailAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.TrustedEmails.FindAsync(id);
        if (entry == null) return false;
        db.TrustedEmails.Remove(entry);
        await db.SaveChangesAsync();
        return true;
    }
}
