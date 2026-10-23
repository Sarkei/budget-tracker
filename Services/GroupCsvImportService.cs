using BudgetTracker.Models;
using BudgetTracker.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace BudgetTracker.Services;

/// <summary>A row from a group expense CSV import.</summary>
public class GroupImportRow
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string PaidByUsername { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Note { get; set; }

    public bool IsSelected { get; set; } = true;
    public string? ValidationError { get; set; }
}

public class GroupImportResult
{
    public List<GroupImportRow> Rows { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class GroupCsvImportService
{
    private static readonly CultureInfo _de = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Parse a CSV file for group expense history import.
    /// Expected format: Date;Description;PaidByUsername;TotalAmount;Currency;Note
    /// Date format: dd.MM.yyyy or yyyy-MM-dd
    /// Example: 01.01.2024;Dinner;john;45.50;EUR;Birthday celebration
    /// </summary>
    public GroupImportResult ParseCsv(Stream csvStream)
    {
        var result = new GroupImportResult();
        var rows = new List<GroupImportRow>();

        using (var reader = new StreamReader(csvStream, Encoding.UTF8))
        {
            string? line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                // Skip empty lines and header line (if first line contains "Datum" or "Date")
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (lineNumber == 1 && (line.Contains("Datum", StringComparison.OrdinalIgnoreCase) || 
                    line.Contains("Date", StringComparison.OrdinalIgnoreCase)))
                    continue;

                var parts = line.Split(new[] { ';' }, StringSplitOptions.None);

                if (parts.Length < 4)
                {
                    result.Warnings.Add($"Zeile {lineNumber}: Ungültiges Format (zu wenig Felder). Ignoriert.");
                    continue;
                }

                var row = new GroupImportRow();
                var error = new List<string>();

                // Parse Date
                if (!TryParseDate(parts[0].Trim(), out var date))
                {
                    error.Add($"Ungültiges Datum: '{parts[0].Trim()}'");
                }
                else
                {
                    row.Date = date;
                }

                // Parse Description
                row.Description = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(row.Description))
                    error.Add("Beschreibung ist leer");

                // Parse PaidByUsername
                row.PaidByUsername = parts[2].Trim();
                if (string.IsNullOrWhiteSpace(row.PaidByUsername))
                    error.Add("Benutzername (PaidBy) ist leer");

                // Parse Amount
                if (!TryParseAmount(parts[3].Trim(), out var amount))
                {
                    error.Add($"Ungültiger Betrag: '{parts[3].Trim()}'");
                }
                else if (amount <= 0)
                {
                    error.Add("Betrag muss > 0 sein");
                }
                else
                {
                    row.TotalAmount = amount;
                }

                // Parse Currency (optional)
                if (parts.Length > 4)
                {
                    var currency = parts[4].Trim();
                    if (!string.IsNullOrWhiteSpace(currency) && currency.Length <= 10)
                        row.Currency = currency.ToUpper();
                }

                // Parse Note (optional)
                if (parts.Length > 5)
                {
                    var note = parts[5].Trim();
                    if (!string.IsNullOrWhiteSpace(note))
                        row.Note = note;
                }

                if (error.Any())
                {
                    row.ValidationError = string.Join("; ", error);
                    row.IsSelected = false;
                }

                rows.Add(row);
            }
        }

        result.Rows = rows;

        // Warnings for empty import
        if (rows.Count == 0)
            result.Warnings.Add("Die CSV-Datei enthält keine gültigen Zeilen.");

        return result;
    }

    /// <summary>
    /// Import parsed rows into the database as GroupExpense records with IsImported=true.
    /// </summary>
    public async Task<(int ImportedCount, List<string> Errors)> ImportRowsAsync(
        AppDbContext db,
        SharedGroup group,
        List<GroupImportRow> selectedRows,
        Dictionary<string, User> userCache)
    {
        var errors = new List<string>();
        int importedCount = 0;

        foreach (var row in selectedRows)
        {
            if (!string.IsNullOrEmpty(row.ValidationError))
                continue; // Skip rows with validation errors

            try
            {
                // Find the user who paid
                if (!userCache.TryGetValue(row.PaidByUsername.ToLower(), out var paidByUser))
                {
                    errors.Add($"Benutzer '{row.PaidByUsername}' nicht gefunden (Zeile: {row.Description})");
                    continue;
                }

                // Verify user is member of group
                var isMember = await db.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id);

                if (!isMember)
                {
                    errors.Add($"Benutzer '{row.PaidByUsername}' ist nicht Mitglied der Gruppe '{group.Name}'");
                    continue;
                }

                // Create expense
                var expense = new GroupExpense
                {
                    GroupId = group.Id,
                    PaidByUserId = paidByUser.Id,
                    Description = row.Description,
                    TotalAmount = row.TotalAmount,
                    Currency = row.Currency,
                    Date = row.Date,
                    Note = row.Note,
                    IsImported = true,
                    IsRecurring = false,
                    IsIncome = false,
                    CreatedAt = DateTime.UtcNow
                };

                // Create equal split among all group members
                var members = await db.GroupMembers
                    .Where(gm => gm.GroupId == group.Id)
                    .ToListAsync();

                if (members.Count == 0)
                {
                    errors.Add($"Gruppe '{group.Name}' hat keine Mitglieder");
                    continue;
                }

                var splitAmount = row.TotalAmount / members.Count;

                expense.Splits = members.Select(m => new ExpenseSplit
                {
                    UserId = m.UserId,
                    Amount = splitAmount,
                    IsSettled = false
                }).ToList();

                db.GroupExpenses.Add(expense);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Fehler beim Import '{row.Description}': {ex.Message}");
            }
        }

        return (importedCount, errors);
    }

    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(dateStr))
            return false;

        // Try dd.MM.yyyy
        if (DateTime.TryParseExact(dateStr, "dd.MM.yyyy", _de, DateTimeStyles.None, out date))
            return true;

        // Try yyyy-MM-dd
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        // Try dd.MM.yy
        if (DateTime.TryParseExact(dateStr, "dd.MM.yy", _de, DateTimeStyles.None, out date))
            return true;

        return false;
    }

    private static bool TryParseAmount(string amountStr, out decimal amount)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        // Try German format (1.234,56)
        if (decimal.TryParse(amountStr, NumberStyles.Any, _de, out amount))
            return true;

        // Try invariant format (1234.56)
        if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            return true;

        return false;
    }
}
