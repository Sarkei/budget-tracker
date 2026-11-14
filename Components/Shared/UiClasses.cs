using BudgetTracker.Models;

namespace BudgetTracker.Components.Shared;

/// <summary>CSS-Klassen für dynamische Zustände (Fitness-Tracker-Tokens).</summary>
public static class UiClasses
{
    public static string BalanceText(decimal balance) =>
        balance > 0 ? "bt-text-success" : balance < 0 ? "bt-text-error" : "bt-text-muted";

    public static string BalanceText(decimal balance, decimal epsilon = 0.005m) =>
        balance > epsilon ? "bt-text-success" : balance < -epsilon ? "bt-text-error" : "bt-text-muted";

    public static string UrgentText(int daysLeft) =>
        daysLeft <= 3 ? "bt-text-error" : daysLeft <= 7 ? "bt-text-warning" : "bt-text-muted";

    public static string TransactionAmount(TransactionType type) =>
        type == TransactionType.Income ? "bt-text-success" : "bt-text-error";

    public static string DaysLeftSuffix(int daysLeft) =>
        daysLeft switch
        {
            0 => " (heute)",
            1 => " (morgen)",
            _ => " (in " + daysLeft + " Tagen)"
        };
}
