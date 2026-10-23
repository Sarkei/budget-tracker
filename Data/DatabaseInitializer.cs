using BudgetTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BudgetTracker.Data;

/// <summary>
/// Seeds the database with default data on first run.
/// Creates the default account and standard categories.
/// </summary>
public static class DatabaseInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // Ensure database schema is created (no-op if db already exists)
        context.Database.EnsureCreated();

        // Create any new tables that may not exist in an older database
        EnsureNewTablesExist(context);

        SeedCategories(context);
        SeedDefaultAccount(context);
    }

    /// <summary>
    /// Creates tables added after initial release using CREATE TABLE IF NOT EXISTS.
    /// EnsureCreated() skips schema updates on existing databases, so we handle
    /// new tables explicitly here.
    /// </summary>
    private static void EnsureNewTablesExist(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id""           INTEGER NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY AUTOINCREMENT,
                ""Username""     TEXT    NOT NULL,
                ""FirstName""    TEXT    NOT NULL DEFAULT '',
                ""LastName""     TEXT    NOT NULL DEFAULT '',
                ""PasswordHash"" TEXT    NOT NULL,
                ""DisplayName""  TEXT,
                ""Role""         INTEGER NOT NULL DEFAULT 1,
                ""IsActive""     INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt""    TEXT    NOT NULL DEFAULT (datetime('now'))
            )");

        context.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username"" ON ""Users"" (""Username"")");

        // Add new columns to Users if they don't exist yet (upgrade path for older databases)
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""FirstName"" TEXT NOT NULL DEFAULT ''"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""LastName""  TEXT NOT NULL DEFAULT ''"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""IsActive""  INTEGER NOT NULL DEFAULT 1"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Accounts"" ADD COLUMN ""AccountType"" TEXT NOT NULL DEFAULT 'Girokonto'"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Accounts"" ADD COLUMN ""IsDefault"" INTEGER NOT NULL DEFAULT 0"); } catch { }

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""AccountMembers"" (
                ""Id""        INTEGER NOT NULL CONSTRAINT ""PK_AccountMembers"" PRIMARY KEY AUTOINCREMENT,
                ""AccountId"" INTEGER NOT NULL,
                ""UserId""    INTEGER NOT NULL,
                ""Role""      INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT ""FK_AccountMembers_Accounts_AccountId"" FOREIGN KEY (""AccountId"") REFERENCES ""Accounts"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_AccountMembers_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            )");

        context.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AccountMembers_AccountId_UserId"" ON ""AccountMembers"" (""AccountId"", ""UserId"")");

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""SavingsPots"" (
                ""Id""            INTEGER NOT NULL CONSTRAINT ""PK_SavingsPots"" PRIMARY KEY AUTOINCREMENT,
                ""Name""          TEXT    NOT NULL,
                ""Description""   TEXT    NOT NULL DEFAULT '',
                ""TargetAmount""  TEXT    NOT NULL DEFAULT '0',
                ""CurrentAmount"" TEXT    NOT NULL DEFAULT '0',
                ""ColorHex""      TEXT    NOT NULL DEFAULT '#42A5F5',
                ""Icon""          TEXT    NOT NULL DEFAULT 'PiggyBank',
                ""IsActive""      INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt""     TEXT    NOT NULL DEFAULT (datetime('now'))
            )");
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""SavingsPots"" ADD COLUMN ""IsActive"" INTEGER NOT NULL DEFAULT 1"); } catch { }

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""SharedGroups"" (
                ""Id""              INTEGER NOT NULL CONSTRAINT ""PK_SharedGroups"" PRIMARY KEY AUTOINCREMENT,
                ""Name""            TEXT    NOT NULL,
                ""Description""     TEXT    NOT NULL DEFAULT '',
                ""ColorHex""        TEXT    NOT NULL DEFAULT '#42A5F5',
                ""CreatedByUserId"" INTEGER NOT NULL,
                ""IsActive""        INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt""       TEXT    NOT NULL DEFAULT (datetime('now')),
                CONSTRAINT ""FK_SharedGroups_Users_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
            )");
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""SharedGroups"" ADD COLUMN ""IsActive"" INTEGER NOT NULL DEFAULT 1"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""SharedGroups"" ADD COLUMN ""Slug"" TEXT NOT NULL DEFAULT ''"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SharedGroups_Slug"" ON ""SharedGroups"" (""Slug"") WHERE ""Slug"" != ''"); } catch { }

        // Backfill Slug for any existing groups that don't have one yet
        BackfillGroupSlugs(context);

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GroupMembers"" (
                ""Id""        INTEGER NOT NULL CONSTRAINT ""PK_GroupMembers"" PRIMARY KEY AUTOINCREMENT,
                ""GroupId""   INTEGER NOT NULL,
                ""UserId""    INTEGER NOT NULL,
                ""Role""      INTEGER NOT NULL DEFAULT 1,
                ""JoinedAt""  TEXT    NOT NULL DEFAULT (datetime('now')),
                CONSTRAINT ""FK_GroupMembers_SharedGroups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""SharedGroups"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_GroupMembers_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            )");

        context.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_GroupMembers_GroupId_UserId"" ON ""GroupMembers"" (""GroupId"", ""UserId"")");
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupMembers"" ADD COLUMN ""JoinedAt"" TEXT NOT NULL DEFAULT (datetime('now'))"); } catch { }

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GroupExpenses"" (
                ""Id""            INTEGER NOT NULL CONSTRAINT ""PK_GroupExpenses"" PRIMARY KEY AUTOINCREMENT,
                ""GroupId""       INTEGER NOT NULL,
                ""PaidByUserId""  INTEGER NOT NULL,
                ""Description""   TEXT    NOT NULL,
                ""TotalAmount""   TEXT    NOT NULL DEFAULT '0',
                ""Currency""      TEXT    NOT NULL DEFAULT 'EUR',
                ""Date""          TEXT    NOT NULL DEFAULT (datetime('now')),
                ""CategoryId""    INTEGER,
                ""Note""          TEXT,
                ""CreatedAt""     TEXT    NOT NULL DEFAULT (datetime('now')),
                CONSTRAINT ""FK_GroupExpenses_SharedGroups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""SharedGroups"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_GroupExpenses_Users_PaidByUserId"" FOREIGN KEY (""PaidByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
            )");
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""Currency"" TEXT NOT NULL DEFAULT 'EUR'"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""Note"" TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""CreatedAt"" TEXT NOT NULL DEFAULT (datetime('now'))"); } catch { }

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ExpenseSplits"" (
                ""Id""             INTEGER NOT NULL CONSTRAINT ""PK_ExpenseSplits"" PRIMARY KEY AUTOINCREMENT,
                ""GroupExpenseId"" INTEGER NOT NULL,
                ""UserId""         INTEGER NOT NULL,
                ""Amount""         TEXT    NOT NULL DEFAULT '0',
                ""IsSettled""      INTEGER NOT NULL DEFAULT 0,
                ""SettledAt""      TEXT,
                CONSTRAINT ""FK_ExpenseSplits_GroupExpenses_GroupExpenseId"" FOREIGN KEY (""GroupExpenseId"") REFERENCES ""GroupExpenses"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_ExpenseSplits_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            )");
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ExpenseSplits"" ADD COLUMN ""SettledAt"" TEXT"); } catch { }

        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""UserPreferences"" (
                ""Id""                    INTEGER NOT NULL CONSTRAINT ""PK_UserPreferences"" PRIMARY KEY AUTOINCREMENT,
                ""UserId""                INTEGER NOT NULL,
                ""ShowSavingsPots""       INTEGER NOT NULL DEFAULT 1,
                ""ShowRecurring""         INTEGER NOT NULL DEFAULT 1,
                ""ShowScheduledPayments"" INTEGER NOT NULL DEFAULT 1,
                ""ShowGroups""            INTEGER NOT NULL DEFAULT 1,
                ""ShowAccountMiniBoxes""  INTEGER NOT NULL DEFAULT 1,
                ""CompactMode""           INTEGER NOT NULL DEFAULT 0,
                ""Currency""              TEXT    NOT NULL DEFAULT 'EUR',
                ""DateFormat""            TEXT    NOT NULL DEFAULT 'dd.MM.yyyy',
                ""Language""              TEXT    NOT NULL DEFAULT 'de',
                ""EnableBudgetWarnings""  INTEGER NOT NULL DEFAULT 0,
                ""MonthlyBudgetLimit""    TEXT    NOT NULL DEFAULT '0',
                ""UpdatedAt""             TEXT    NOT NULL DEFAULT (datetime('now'))
            )");
        try { context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserPreferences_UserId"" ON ""UserPreferences"" (""UserId"")"); } catch { }

        // GroupInvitations table
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GroupInvitations"" (
                ""Id""              INTEGER NOT NULL CONSTRAINT ""PK_GroupInvitations"" PRIMARY KEY AUTOINCREMENT,
                ""GroupId""         INTEGER NOT NULL,
                ""InvitedByUserId"" INTEGER NOT NULL,
                ""InviteeEmail""    TEXT    NOT NULL,
                ""InviteeUserId""   INTEGER,
                ""Status""          INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt""       TEXT    NOT NULL DEFAULT (datetime('now')),
                ""RespondedAt""     TEXT,
                CONSTRAINT ""FK_GroupInvitations_SharedGroups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""SharedGroups"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_GroupInvitations_Users_InvitedByUserId"" FOREIGN KEY (""InvitedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
            )");

        // GroupSettlements table
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GroupSettlements"" (
                ""Id""           INTEGER NOT NULL CONSTRAINT ""PK_GroupSettlements"" PRIMARY KEY AUTOINCREMENT,
                ""GroupId""      INTEGER NOT NULL,
                ""PayerUserId""  INTEGER NOT NULL,
                ""PayeeUserId""  INTEGER NOT NULL,
                ""Amount""       TEXT    NOT NULL DEFAULT '0',
                ""Note""         TEXT,
                ""Date""         TEXT    NOT NULL DEFAULT (datetime('now')),
                ""CreatedAt""    TEXT    NOT NULL DEFAULT (datetime('now')),
                CONSTRAINT ""FK_GroupSettlements_SharedGroups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""SharedGroups"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_GroupSettlements_Users_PayerUserId"" FOREIGN KEY (""PayerUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
                CONSTRAINT ""FK_GroupSettlements_Users_PayeeUserId"" FOREIGN KEY (""PayeeUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
            )");

        // New columns on GroupExpenses for recurring support
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""IsRecurring"" INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""RecurringInterval"" INTEGER"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""GroupExpenses"" ADD COLUMN ""IsIncome"" INTEGER NOT NULL DEFAULT 0"); } catch { }

        // LastLoginAt column on Users
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""LastLoginAt"" TEXT"); } catch { }

        // ── Upgrade path: columns added after initial release ──────────────────

        // UserPreferences: budget warning columns (may not exist in older DBs)
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""UserPreferences"" ADD COLUMN ""EnableBudgetWarnings"" INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""UserPreferences"" ADD COLUMN ""MonthlyBudgetLimit""    TEXT    NOT NULL DEFAULT '0'"); } catch { }

        // Transactions: foreign currency + link columns (may not exist in older DBs)
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Transactions"" ADD COLUMN ""OriginalAmount""      TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Transactions"" ADD COLUMN ""OriginalCurrency""    TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Transactions"" ADD COLUMN ""ExchangeRate""        TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Transactions"" ADD COLUMN ""RecurringItemId""     INTEGER"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Transactions"" ADD COLUMN ""ScheduledPaymentId"" INTEGER"); } catch { }

        // ScheduledPayments: foreign currency columns (may not exist in older DBs)
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ScheduledPayments"" ADD COLUMN ""OriginalAmount""   TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ScheduledPayments"" ADD COLUMN ""OriginalCurrency"" TEXT"); } catch { }
        try { context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ScheduledPayments"" ADD COLUMN ""ExchangeRate""     TEXT"); } catch { }

        // TrustedEmails table
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""TrustedEmails"" (
                ""Id""            INTEGER NOT NULL CONSTRAINT ""PK_TrustedEmails"" PRIMARY KEY AUTOINCREMENT,
                ""Email""         TEXT    NOT NULL,
                ""AddedByUserId"" INTEGER NOT NULL DEFAULT 0,
                ""AddedAt""       TEXT    NOT NULL DEFAULT (datetime('now'))
            )");
        try { context.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TrustedEmails_Email"" ON ""TrustedEmails"" (""Email"")"); } catch { }
    }

    private static void BackfillGroupSlugs(AppDbContext context)
    {
        var groups = context.SharedGroups.Where(g => g.Slug == "").ToList();
        foreach (var g in groups)
        {
            var baseSlug = Slugify(g.Name);
            var slug = baseSlug;
            var suffix = 1;
            while (context.SharedGroups.Any(x => x.Slug == slug && x.Id != g.Id))
            {
                suffix++;
                slug = $"{baseSlug}-{suffix}";
            }
            g.Slug = slug;
        }
        if (groups.Count > 0) context.SaveChanges();
    }

    internal static string Slugify(string name)
    {
        var s = name.Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9]+", "-");
        return s.Trim('-');
    }

    private static void SeedCategories(AppDbContext context)
    {
        if (context.Categories.Any()) return;

        var categories = new[]
        {
            // Income categories
            new Category { Name = "Gehalt",          Icon = "Briefcase",       ColorHex = "#66BB6A", Type = CategoryType.Income,   IsDefault = true },
            new Category { Name = "Nebeneinkommen",  Icon = "CurrencyEur",     ColorHex = "#26A69A", Type = CategoryType.Income,   IsDefault = true },
            new Category { Name = "Investitionen",   Icon = "TrendingUp",      ColorHex = "#29B6F6", Type = CategoryType.Income,   IsDefault = true },
            new Category { Name = "Sonstiges",       Icon = "DotsHorizontal",  ColorHex = "#78909C", Type = CategoryType.Both,     IsDefault = true },

            // Expense categories - Housing
            new Category { Name = "Wohnen",          Icon = "Home",            ColorHex = "#42A5F5", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Strom & Gas",     Icon = "Lightning",       ColorHex = "#FFA726", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Internet & TV",   Icon = "Wifi",            ColorHex = "#5C6BC0", Type = CategoryType.Expense,  IsDefault = true },

            // Expense categories - Daily life
            new Category { Name = "Lebensmittel",    Icon = "Cart",            ColorHex = "#EF5350", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Restaurant",      Icon = "Food",            ColorHex = "#FF7043", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Transport",       Icon = "Car",             ColorHex = "#42A5F5", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Gesundheit",      Icon = "Hospital",        ColorHex = "#EC407A", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Kleidung",        Icon = "Tshirt",          ColorHex = "#AB47BC", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Freizeit",        Icon = "Controller",      ColorHex = "#26C6DA", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Urlaub",          Icon = "Airplane",        ColorHex = "#FFCA28", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Shopping",        Icon = "ShoppingOutline", ColorHex = "#FF80AB", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Versicherungen",  Icon = "Shield",          ColorHex = "#8D6E63", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Abonnements",     Icon = "Refresh",         ColorHex = "#4CAF50", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Bildung",         Icon = "School",          ColorHex = "#00BCD4", Type = CategoryType.Expense,  IsDefault = true },
            new Category { Name = "Ratenzahlungen",  Icon = "CalendarClock",   ColorHex = "#FF6F00", Type = CategoryType.Expense,  IsDefault = true },
        };

        context.Categories.AddRange(categories);
        context.SaveChanges();
    }

    private static void SeedDefaultAccount(AppDbContext context)
    {
        if (context.Accounts.Any()) return;

        var defaultAccount = new Account
        {
            Name = "Girokonto",
            ColorHex = "#42A5F5",
            Icon = "Bank",
            InitialBalance = 0m,
            Currency = "EUR",
            IsActive = true
        };

        context.Accounts.Add(defaultAccount);
        context.SaveChanges();
    }
}
