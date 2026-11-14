namespace BudgetTracker.Services;

/// <summary>
/// Singleton service: notifies Blazor circuits in real-time.
/// Supports two patterns:
///   1. Per-user bell callbacks (invite notifications in MainLayout)
///   2. Per-(user,group) group-detail callbacks (expense/member changes in GroupDetail)
/// </summary>
public sealed class GroupNotificationService
{
    private readonly object _lock = new();

    // userId → callback for notification bell (invite received)
    private readonly Dictionary<int, Func<Task>> _bellCallbacks = new();

    // (userId, groupId) → callback for group detail page
    private readonly Dictionary<(int UserId, int GroupId), Func<Task>> _groupCallbacks = new();

    // userId → callback for user's own Settings page (profile/email changed by admin)
    private readonly Dictionary<int, Func<Task>> _profileCallbacks = new();

    // ── Bell (invite) ──────────────────────────────────────────────────────────

    public void Register(int userId, Func<Task> callback)
    {
        lock (_lock) _bellCallbacks[userId] = callback;
    }

    public void Unregister(int userId)
    {
        lock (_lock) _bellCallbacks.Remove(userId);
    }

    public async Task NotifyUserAsync(int recipientUserId)
    {
        Func<Task>? cb;
        lock (_lock) _bellCallbacks.TryGetValue(recipientUserId, out cb);
        if (cb != null) try { await cb(); } catch { }
    }

    // ── Group detail ───────────────────────────────────────────────────────────

    public void RegisterGroupDetail(int userId, int groupId, Func<Task> callback)
    {
        lock (_lock) _groupCallbacks[(userId, groupId)] = callback;
    }

    public void UnregisterGroupDetail(int userId, int groupId)
    {
        lock (_lock) _groupCallbacks.Remove((userId, groupId));
    }

    /// <summary>
    /// Notify all members of <paramref name="groupId"/> that currently have the group detail page open,
    /// except optionally the caller (<paramref name="excludeKicked"/> = -1 to notify everyone).
    /// </summary>
    public async Task NotifyGroupAsync(int groupId, int excludeKicked = -1)
    {
        List<Func<Task>> cbs;
        lock (_lock)
        {
            cbs = _groupCallbacks
                .Where(kv => kv.Key.GroupId == groupId && kv.Key.UserId != excludeKicked)
                .Select(kv => kv.Value)
                .ToList();
        }
        foreach (var cb in cbs)
            try { await cb(); } catch { }
    }

    /// <summary>
    /// Notify a single user who was kicked — their group page will navigate away.
    /// Reuses the group detail callback; the component decides what "reload" means when it no longer finds itself as member.
    /// </summary>
    public async Task NotifyKickedUserAsync(int kickedUserId, int groupId)
    {
        Func<Task>? cb;
        lock (_lock) _groupCallbacks.TryGetValue((kickedUserId, groupId), out cb);
        if (cb != null) try { await cb(); } catch { }
    }

    /// <summary>
    /// Notify all group detail pages where <paramref name="userId"/> is a member
    /// (e.g. after a profile name change so member names refresh live).
    /// Also notifies the user's own Settings page if it is open.
    /// </summary>
    public async Task NotifyUserProfileChangedAsync(int userId)
    {
        List<Func<Task>> cbs;
        Func<Task>? profileCb;
        lock (_lock)
        {
            cbs = _groupCallbacks.Select(kv => kv.Value).ToList();
            _profileCallbacks.TryGetValue(userId, out profileCb);
        }
        foreach (var cb in cbs)
            try { await cb(); } catch { }
        if (profileCb != null)
            try { await profileCb(); } catch { }
    }

    // ── Per-user profile page callbacks ────────────────────────────────────────

    public void RegisterProfileUpdate(int userId, Func<Task> callback)
    {
        lock (_lock) _profileCallbacks[userId] = callback;
    }

    public void UnregisterProfileUpdate(int userId)
    {
        lock (_lock) _profileCallbacks.Remove(userId);
    }
}
