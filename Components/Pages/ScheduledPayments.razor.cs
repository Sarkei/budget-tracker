using BudgetTracker.Components.Dialogs;
using BudgetTracker.Data;
using BudgetTracker.Models;
using BudgetTracker.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace BudgetTracker.Components.Pages;

public partial class ScheduledPayments : IDisposable
{
    private static readonly System.Globalization.CultureInfo _de =
        System.Globalization.CultureInfo.GetCultureInfo("de-DE");

    private bool _loading;
    private List<ScheduledPayment> _upcomingPayments = new();
    private List<ScheduledPayment> _paidPayments = new();
    private decimal _totalUpcoming;
    private decimal _totalNext30Days;

    protected override async Task OnInitializedAsync()
    {
        AppState.OnAccountChanged += OnAccountChanged;
        await LoadAsync();
    }

    private async void OnAccountChanged()
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        await using var db = await DbFactory.CreateDbContextAsync();

        var q = db.ScheduledPayments.Include(s => s.Category).AsQueryable();
        if (AppState.SelectedAccountId.HasValue)
            q = q.Where(s => s.AccountId == AppState.SelectedAccountId.Value);

        var all = await q.ToListAsync();
        _upcomingPayments = all.Where(s => !s.IsPaid).OrderBy(s => s.DueDate).ToList();
        _paidPayments = all.Where(s => s.IsPaid)
            .OrderByDescending(s => s.PaidAt ?? s.DueDate)
            .Take(20)
            .ToList();

        _totalUpcoming = _upcomingPayments
            .Where(p => p.Type == TransactionType.Expense)
            .Sum(p => p.Amount);

        var cutoff = DateTime.Today.AddDays(30);
        _totalNext30Days = _upcomingPayments
            .Where(p => p.DueDate <= cutoff && p.Type == TransactionType.Expense)
            .Sum(p => p.Amount);

        _loading = false;
    }

    private async Task AddPaymentAsync()
    {
        var parameters = new DialogParameters { ["AccountId"] = AppState.SelectedAccountId };
        var dlg = await DialogService.ShowAsync<ScheduledPaymentDialog>(
            "Geplante Zahlung hinzufügen", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (!(await dlg.Result)!.Canceled)
        {
            AppState.NotifyTransactionChanged();
            await LoadAsync();
        }
    }

    private async Task EditPaymentAsync(ScheduledPayment payment)
    {
        var parameters = new DialogParameters
        {
            ["ExistingPayment"] = payment,
            ["AccountId"] = AppState.SelectedAccountId
        };
        var dlg = await DialogService.ShowAsync<ScheduledPaymentDialog>(
            "Zahlung bearbeiten", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (!(await dlg.Result)!.Canceled)
        {
            AppState.NotifyTransactionChanged();
            await LoadAsync();
        }
    }

    private async Task DeletePaymentAsync(ScheduledPayment payment)
    {
        var confirm = await DialogService.ShowMessageBox(
            "Löschen?",
            "\"" + payment.Name + "\" wirklich löschen?",
            yesText: "Löschen",
            cancelText: "Abbrechen");
        if (confirm != true) return;

        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.ScheduledPayments.FindAsync(payment.Id);
        if (entity != null)
        {
            db.ScheduledPayments.Remove(entity);
            await db.SaveChangesAsync();
        }

        AppState.NotifyTransactionChanged();
        await LoadAsync();
    }

    private async Task MarkPaidAsync(ScheduledPayment payment)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var entity = await db.ScheduledPayments.FindAsync(payment.Id);
        if (entity == null || entity.IsPaid) return;

        entity.IsPaid = true;
        entity.PaidAt = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            AccountId = entity.AccountId,
            CategoryId = entity.CategoryId,
            Amount = entity.Amount,
            OriginalAmount = entity.OriginalAmount,
            OriginalCurrency = entity.OriginalCurrency,
            ExchangeRate = entity.ExchangeRate,
            Type = entity.Type,
            Date = entity.DueDate.Date <= DateTime.Today ? entity.DueDate.Date : DateTime.Today,
            Note = entity.Name,
            ScheduledPaymentId = entity.Id
        });

        await db.SaveChangesAsync();
        AppState.NotifyTransactionChanged();
        await LoadAsync();
    }

    private static string CategoryLabel(ScheduledPayment p) => p.Category?.Name ?? "-";

    public void Dispose() => AppState.OnAccountChanged -= OnAccountChanged;
}
