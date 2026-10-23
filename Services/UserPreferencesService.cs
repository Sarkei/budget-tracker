using BudgetTracker.Data;
using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

/// <summary>
/// Scoped service: loads and caches user preferences for the current circuit.
/// Other components can subscribe to OnChanged to re-render when preferences are updated.
/// </summary>
public class UserPreferencesService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private UserPreferences? _cached;

    public event Action? OnChanged;

    public UserPreferencesService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // Returns cached preferences if loaded, otherwise loads defaults (not persisted until user changes something)
    public UserPreferences Current => _cached ?? new UserPreferences();

    public async Task LoadAsync(int userId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            _cached = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (_cached == null)
            {
                // First time: create default row
                _cached = new UserPreferences { UserId = userId };
                db.UserPreferences.Add(_cached);
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // Fallback to in-memory defaults — avoids crashing the circuit
            // when the UserPreferences table is missing new columns (schema upgrade path)
            _cached ??= new UserPreferences { UserId = userId };
        }
    }

    public async Task SaveAsync()
    {
        if (_cached == null) return;
        _cached.UpdatedAt = DateTime.UtcNow;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == _cached.UserId);
        if (entity == null)
        {
            db.UserPreferences.Add(_cached);
        }
        else
        {
            entity.ShowSavingsPots       = _cached.ShowSavingsPots;
            entity.ShowRecurring         = _cached.ShowRecurring;
            entity.ShowScheduledPayments = _cached.ShowScheduledPayments;
            entity.ShowGroups            = _cached.ShowGroups;
            entity.ShowAccountMiniBoxes  = _cached.ShowAccountMiniBoxes;
            entity.CompactMode           = _cached.CompactMode;
            entity.Currency              = _cached.Currency;
            entity.DateFormat            = _cached.DateFormat;
            entity.Language              = _cached.Language;
            entity.EnableBudgetWarnings  = _cached.EnableBudgetWarnings;
            entity.MonthlyBudgetLimit    = _cached.MonthlyBudgetLimit;
            entity.UpdatedAt             = _cached.UpdatedAt;
        }
        await db.SaveChangesAsync();
        OnChanged?.Invoke();
    }
}
