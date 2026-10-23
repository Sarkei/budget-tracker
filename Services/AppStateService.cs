namespace BudgetTracker.Services;

/// <summary>
/// Holds the currently selected account across all pages (scoped per Blazor circuit).
/// </summary>
public class AppStateService
{
    public int? SelectedAccountId { get; private set; }
    public bool ShowingAllAccounts => !SelectedAccountId.HasValue;

    public event Action? OnAccountChanged;
    public event Action? OnGroupsChanged;
    public event Action? OnTransactionChanged;

    public void SelectAccount(int? accountId)
    {
        SelectedAccountId = accountId;
        OnAccountChanged?.Invoke();
    }

    public void NotifyGroupsChanged() => OnGroupsChanged?.Invoke();
    public void NotifyTransactionChanged() => OnTransactionChanged?.Invoke();
}
