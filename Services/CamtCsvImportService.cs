using BudgetTracker.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BudgetTracker.Services;

/// <summary>Parsed row from a CAMT CSV bank-statement export.</summary>
public class CamtRow
{
    public DateTime BookingDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string BookingText { get; set; } = "";
    public string Counterparty { get; set; } = "";
    public string CounterpartyIban { get; set; } = "";
    public string Purpose { get; set; } = "";
    /// <summary>Positive = credit, negative = debit.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";

    // Set during preview
    public int? SuggestedCategoryId { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsDuplicate { get; set; }
}

public class CamtImportResult
{
    public List<CamtRow> Rows { get; set; } = new();
    public string DetectedBank { get; set; } = "Unbekannte Bank";
    public string SourceIban { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
}

public class CamtCsvImportService
{
    private static readonly CultureInfo _de = CultureInfo.GetCultureInfo("de-DE");

    // Keyword-based category auto-assignment rules
    private static readonly (string[] Keywords, string CategoryHint)[] CategoryRules =
    [
        (["EDEKA", "REWE", "ALDI", "LIDL", "NETTO", "PENNY", "KAUFLAND", "ROSSMANN", " DM ", "DM-MARKT",
          "MUELLER DROGERIE", "SPAR ", "NORMA", "TEGUT", "GLOBUS", "E-CENTER", "NAHKAUF", "COMBI ",
          "FAMILA", "MARKTKAUF", "VOLLCORNER", "BIO COMPANY", "BASIC BIO", "DENN'S"],
         "Lebensmittel"),

        (["NETFLIX", "AMAZON PRIME", "SPOTIFY", "DISNEY+", "APPLE.COM/BILL", "GOOGLE ONE",
          "DAZN", "SKY TICKET", "MAXDOME", "RTL+", "MAGENTA MUSIK", "TIDAL ", "AUDIBLE ",
          "AMAZON MUSIC", "YOUTUBE PREMIUM", "TWITCH "],
         "Abonnements"),

        (["ARAL ", "SHELL ", "JET ", "TOTAL ", "AGIP", "ESSO ", "STAR TANKSTELLE",
          "OMV ", "BFT ", "WESTFALEN", "ORLEN", "HEM "],
         "Kraftstoff"),

        (["DB BAHN", "DEUTSCHE BAHN", "MVV ", "HVV ", "VBB ", "BVG ", "FLIXBUS",
          "RYANAIR", "LUFTHANSA", "EASYJET", "EUROWINGS", "WIZZ AIR", "CONDOR ",
          "UBER ", "FREE NOW", "MYTAXI", "SIXT ", "EUROPCAR", "DEUTSCHEBAHN"],
         "Transport"),

        (["APOTHEKE", "ZAHNARZT", "ARZTPRAXIS", "KRANKENHAUS", "PHYSIOTHERAPIE",
          "ORTHOPAEDIE", "AUGENARZT", "HAUTARZT", "FACHARZT", "KINDERARZT",
          "PRAXIS DR.", "DR.MED."],
         "Gesundheit"),

        (["AMAZON ", "AMAZON.DE", "EBAY ", "ZALANDO", "H&M ", "ZARA ", "C&A ",
          "OTTO ", "MEDIAMARKT", "SATURN ", "IKEA ", "ABOUT YOU", "SHEIN",
          "BONPRIX", "TCHIBO", "KLINGEL", "BAUR ", "MANGO "],
         "Shopping"),

        (["STADTWERKE", "STROM ", "ERDGAS", "SWMUENCHEN", "REWAG ", "VATTENFALL",
          "E.ON ", " EON ", " RWE ", "INNOGY", "MAINOVA", "ENERCITY", "BAYERNWERK",
          "WASSERWERK", "STADTGAS"],
         "Strom/Gas/Wasser"),

        (["TELEKOM", "VODAFONE", "O2 ONLINE", "O2 GERMANY", "1&1 TELECOM",
          "FREENET", "CONGSTAR", "KLARMOBIL", "SIPGATE"],
         "Internet/Telefon"),

        (["MIETE", "KALTMIETE", "WARMMIETE", "HAUSVERWALTUNG", "WEG VERWALTUNG",
          "NEBENKOSTENABRECHNUNG", "GRUNDSTEUER", "WOHNUNGSMIETE"],
         "Miete/Wohnen"),

        (["MCDONALDS", "BURGER KING", "KFC ", "NORDSEE ", "SUBWAY ", "PIZZA HUT",
          "DOMINOS", "VAPIANO", "ALEX ", "L'OSTERIA", "PETER PANE", "HANS IM GLUECK",
          "DEAN & DAVID", "LE CROBAG", "YORMAS", "RESTAURANT ", "PIZZERIA "],
         "Restaurant/Essen"),

        (["GEHALT", "LOHN", "ENTGELT FUER", "ARBEITGEBER", "TARIF LOHN"],
         "Gehalt"),

        (["KINDERGELD", "ELTERNGELD", "WOHNGELD", "SOZIALHILFE", "BUERGERGELD",
          "BAFOEG", "SOZIALLEISTUNG"],
         "Sozialleistungen"),

        (["VERSICHERUNG", "ALLIANZ ", "HUK-COBURG", "ERGO ", "AXA ", "GOTHAER",
          "DEBEKA ", "GENERALI", "R+V ", "ZURICH "],
         "Versicherungen"),

        (["GYM", "FITNESS STUDIO", "FITNESSSTUDIO", "MCFIT", "FIT ONE",
          "CLEVER FIT", "EVO FITNESS", "BASEFIT", "JOHN REED"],
         "Sport/Fitness"),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public CamtImportResult ParseCsv(string csvContent)
    {
        var result = new CamtImportResult();
        if (string.IsNullOrWhiteSpace(csvContent)) return result;

        var lines = csvContent
            .Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n');

        var sep = DetectSeparator(lines);

        // Find the header row (skip meta-info rows that Sparkasse exports before the data)
        int headerIdx = -1;
        string[] headers = [];
        for (int i = 0; i < Math.Min(lines.Length, 15); i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = SplitCsvLine(lines[i], sep)
                .Select(h => h.Trim('"', ' ')).ToArray();
            var joined = string.Join(" ", cells).ToUpperInvariant();
            if (joined.Contains("BUCHUNGSTAG") || joined.Contains("BUCHUNGSDATUM") ||
                joined.Contains("BUCHUNG") && joined.Contains("BETRAG"))
            {
                headerIdx = i;
                headers = cells;
                break;
            }
        }

        if (headerIdx < 0)
        {
            result.Warnings.Add("Keine erkennbare Kopfzeile gefunden. Bitte prüfe das CSV-Format (Sparkasse, DKB, ING, Commerzbank).");
            return result;
        }

        result.DetectedBank = DetectBank(headers);

        // Try to extract source IBAN from pre-header meta rows (Sparkasse style)
        for (int i = 0; i < headerIdx; i++)
        {
            foreach (var cell in SplitCsvLine(lines[i], sep))
            {
                var clean = cell.Trim('"', ' ').Replace(" ", "");
                if (Regex.IsMatch(clean, @"^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$"))
                    result.SourceIban = clean;
            }
        }

        // Build column index map (normalised key → 0-based index)
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            colMap[NormaliseHeader(headers[i])] = i;

        var seen = new HashSet<string>();
        int dupCount = 0;

        for (int li = headerIdx + 1; li < lines.Length; li++)
        {
            var line = lines[li].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = SplitCsvLine(line, sep);
            if (cells.Length < 3) continue;

            string Get(params string[] keys)
            {
                foreach (var k in keys)
                    if (colMap.TryGetValue(k, out var idx) && idx < cells.Length)
                        return cells[idx].Trim('"', ' ');
                return "";
            }

            if (!TryParseGermanDate(Get("buchungstag", "buchungsdatum", "buchung", "datum"), out var bookingDate))
                continue;

            TryParseGermanDate(Get("valutadatum", "wertstellung", "valuta"), out var valueDate);

            if (!TryParseGermanAmount(Get("betrag", "betrageur", "betrag", "umsatz"), out var amount))
                continue;

            var counterparty = Get(
                "auftraggerbeguenstigter", "beguenstigterauftraggeber",
                "zahlungspflichtige*r", "zahlungsempfanger*in",
                "auftraggeber/empfanger", "name");
            var counterpartyIban = Get("kontonummer/iban", "iban", "kontonummer");
            var bookingText = Get("buchungstext", "buchungart", "buchungsart", "umsatzart", "verwendungszweck");
            var purpose = Get("verwendungszweck", "buchungstext");
            var currency = Get("wahrung", "currency");
            if (string.IsNullOrWhiteSpace(currency)) currency = "EUR";

            // Build combined note: counterparty | booking-text | purpose (deduplicated)
            var noteParts = new[] { counterparty, bookingText, purpose }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var note = string.Join(" | ", noteParts);

            // Duplicate detection
            var key = $"{bookingDate:yyyyMMdd}|{amount:F2}|{NormaliseHeader(counterparty)}|{NormaliseHeader(bookingText)}";
            bool isDup = !seen.Add(key);
            if (isDup) dupCount++;

            result.Rows.Add(new CamtRow
            {
                BookingDate = bookingDate,
                ValueDate = valueDate == default ? null : valueDate,
                BookingText = bookingText,
                Counterparty = counterparty,
                CounterpartyIban = counterpartyIban,
                Purpose = note,
                Amount = amount,
                Currency = currency,
                IsDuplicate = isDup,
                IsSelected = !isDup
            });
        }

        if (dupCount > 0)
            result.Warnings.Add($"{dupCount} mögliche Duplikat(e) erkannt – diese sind vorausgewählt deaktiviert.");

        return result;
    }

    public void AssignCategories(CamtImportResult result, List<Category> categories)
    {
        foreach (var row in result.Rows)
            row.SuggestedCategoryId = GuessCategory(row, categories);
    }

    /// <summary>
    /// Enhanced category assignment that combines keyword rules, fuzzy category-name
    /// matching, amount-sign heuristics and a scoring pass so the best match wins.
    /// </summary>
    public void AssignCategoriesEnhanced(CamtImportResult result, List<Category> categories)
    {
        foreach (var row in result.Rows)
            row.SuggestedCategoryId = GuessCategoryEnhanced(row, categories);
    }

    public int? GuessCategoryEnhanced(CamtRow row, List<Category> categories)
    {
        if (categories.Count == 0) return null;

        var text = $"{row.Counterparty} {row.BookingText} {row.Purpose}".ToUpperInvariant();
        bool isIncome = row.Amount > 0;

        // ── 1. Score each known rule ──────────────────────────────────────────
        int bestScore = 0;
        Category? bestCat = null;

        foreach (var (keywords, hint) in CategoryRules)
        {
            int score = 0;
            foreach (var kw in keywords)
            {
                var kwUp = kw.Trim().ToUpperInvariant();
                if (text.Contains(kwUp))
                    score += kwUp.Length >= 8 ? 12 : kwUp.Length >= 5 ? 8 : 5;
            }
            if (score == 0) continue;

            // Find the target category by hint matching
            var cat = MatchCategoryByHint(hint, categories);
            if (cat == null) continue;

            // Penalise income rows that matched an expense rule (avoids Gehalt → Lebensmittel)
            if (isIncome && cat.Type == CategoryType.Expense) score -= 6;
            if (!isIncome && cat.Type == CategoryType.Income) score -= 6;

            if (score > bestScore) { bestScore = score; bestCat = cat; }
        }

        // ── 2. Direct category-name matching against the transaction text ─────
        // e.g. if the user has a category named "Gehalt" and the text says "GEHALT"
        foreach (var cat in categories)
        {
            var catWords = cat.Name.ToUpperInvariant()
                .Split(['/', ' ', '-', '&'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4);
            int score = catWords.Count(w => text.Contains(w)) * 7;

            // Bonus when amount sign matches category type expectation
            if (score > 0)
            {
                if (isIncome  && cat.Type == CategoryType.Income)  score += 4;
                if (!isIncome && cat.Type == CategoryType.Expense) score += 4;
            }

            if (score > bestScore) { bestScore = score; bestCat = cat; }
        }

        // ── 3. IBAN prefix heuristics ─────────────────────────────────────────
        // Savings accounts and known IBANs sometimes missing from counterparty text
        if (bestCat == null && !string.IsNullOrWhiteSpace(row.CounterpartyIban))
        {
            // Internal transfer detection (same country prefix)
            var iban = row.CounterpartyIban.Replace(" ", "").ToUpperInvariant();
            if (iban.StartsWith("DE") && isIncome)
            {
                var sav = categories.FirstOrDefault(c =>
                    c.Name.Contains("Spar", StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Contains("Transfer", StringComparison.OrdinalIgnoreCase));
                if (sav != null) bestCat = sav;
            }
        }

        // ── 4. Amount-sign fallback ───────────────────────────────────────────
        if (bestCat == null)
        {
            if (isIncome)
                bestCat = categories.FirstOrDefault(c =>
                    c.Type == CategoryType.Income ||
                    c.Name.Contains("Einnahme",  StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Contains("Einkommen", StringComparison.OrdinalIgnoreCase));
            // No fallback for expenses – leave null so user assigns manually
        }

        return bestCat?.Id;
    }

    private static Category? MatchCategoryByHint(string hint, List<Category> categories)
    {
        // Exact (case-insensitive) containment in both directions
        var exact = categories.FirstOrDefault(c =>
            c.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
            hint.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Word-by-word partial match
        var hintWords = hint
            .Split(['/', ' ', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3).ToArray();
        return categories.FirstOrDefault(c =>
            hintWords.Any(w => c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    public int? GuessCategory(CamtRow row, List<Category> categories)
    {
        var text = $"{row.Counterparty} {row.BookingText} {row.Purpose}".ToUpperInvariant();

        foreach (var (keywords, hint) in CategoryRules)
        {
            if (keywords.Any(k => text.Contains(k.ToUpperInvariant())))
            {
                var cat = categories.FirstOrDefault(c =>
                    c.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                    hint.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
                if (cat != null) return cat.Id;

                // Try partial word match on any category
                var hintWords = hint.Split('/', ' ').Where(w => w.Length > 3);
                cat = categories.FirstOrDefault(c =>
                    hintWords.Any(w => c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
                if (cat != null) return cat.Id;
            }
        }

        // Amount-based fallback
        if (row.Amount > 0)
        {
            var inc = categories.FirstOrDefault(c =>
                c.Type == CategoryType.Income || c.Name.Contains("Einnahme", StringComparison.OrdinalIgnoreCase));
            return inc?.Id;
        }

        return null;
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static char DetectSeparator(string[] lines)
    {
        foreach (var l in lines.Take(5).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            if (l.Contains(';')) return ';';
            if (l.Count(c => c == ',') > 2) return ',';
        }
        return ';';
    }

    private static string[] SplitCsvLine(string line, char sep)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        bool inQ = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQ = !inQ;
            }
            else if (c == sep && !inQ) { parts.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts.ToArray();
    }

    /// <summary>Normalise a column header to a simple lowercase ASCII key for lookup.</summary>
    private static string NormaliseHeader(string s) =>
        Regex.Replace(
            s.ToLowerInvariant()
             .Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss")
             .Replace("é", "e"),
            @"[\s\*/\-_()\[\]€$]+", "");

    private static bool TryParseGermanDate(string s, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim('"', ' ');
        return DateTime.TryParseExact(s,
            ["dd.MM.yy", "dd.MM.yyyy", "d.M.yyyy", "d.M.yy", "yyyy-MM-dd", "dd/MM/yyyy"],
            _de, DateTimeStyles.None, out result)
            || DateTime.TryParse(s, _de, DateTimeStyles.None, out result);
    }

    private static bool TryParseGermanAmount(string s, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim('"', ' ');

        // German: "1.234,56" or "-1.234,56"
        if (decimal.TryParse(s,
            NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowCurrencySymbol,
            _de, out result))
            return true;

        // Fallback invariant (comma → dot)
        var invariant = Regex.Replace(s, @"[^0-9\-\+\.]", "").TrimStart('+');
        return decimal.TryParse(invariant,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out result);
    }

    private static string DetectBank(string[] headers)
    {
        var j = string.Join(" ", headers).ToUpperInvariant();
        if (j.Contains("AUFTRAGSKONTO") && j.Contains("BEGUENSTIGTER")) return "Sparkasse";
        if (j.Contains("ZAHLUNGSPFLICHTIGE") || j.Contains("ZAHLUNGSEMPFANGER")) return "DKB";
        if (j.Contains("SALDO") && j.Contains("AUFTRAGGEBER/EMPFANGER")) return "ING";
        if (j.Contains("UMSATZART") && j.Contains("AUFTRAGGEBERKONTO")) return "Commerzbank";
        if (j.Contains("BUCHUNGSTEXT") && j.Contains("BEGUENSTIGTER")) return "Volksbank";
        return "Deutsches Kreditinstitut";
    }
}
