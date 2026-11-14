using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BudgetTracker.Services;

/// <summary>
/// Fetches live exchange rates from the Frankfurter API (api.frankfurter.app).
/// Rates are cached in memory for 1 hour to avoid excessive API calls.
/// </summary>
public class CurrencyService
{
    private static readonly HttpClient _http = new HttpClient
    {
        BaseAddress = new Uri("https://api.frankfurter.app/"),
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Cache: base currency -> (timestamp, rates)
    private static readonly Dictionary<string, (DateTime Timestamp, Dictionary<string, decimal> Rates)> _cache = new();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the exchange rate: how many units of <paramref name="toCurrency"/> per 1 unit of <paramref name="fromCurrency"/>.
    /// Returns 1.0 if currencies are equal or on error.
    /// </summary>
    public static async Task<decimal> GetRateAsync(string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        string cacheKey = fromCurrency.ToUpperInvariant();

        if (_cache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.Timestamp < CacheDuration)
        {
            return cached.Rates.TryGetValue(toCurrency.ToUpperInvariant(), out var cachedRate)
                ? cachedRate
                : 1m;
        }

        try
        {
            // Frankfurter API: GET /latest?from=EUR returns all rates vs EUR
            var response = await _http.GetFromJsonAsync<FrankfurterResponse>($"latest?from={fromCurrency}");
            if (response?.Rates is not null)
            {
                _cache[cacheKey] = (DateTime.UtcNow, response.Rates);
                return response.Rates.TryGetValue(toCurrency.ToUpperInvariant(), out var rate)
                    ? rate
                    : 1m;
            }
        }
        catch
        {
            // Network unavailable or API error — return 1 so app still works offline
        }

        return 1m;
    }

    /// <summary>
    /// Converts an amount from one currency to another.
    /// </summary>
    public static async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency)
    {
        decimal rate = await GetRateAsync(fromCurrency, toCurrency);
        return Math.Round(amount * rate, 2);
    }

    /// <summary>
    /// Returns a list of commonly used currency codes.
    /// </summary>
    public static List<string> GetCommonCurrencies() => new()
    {
        "EUR", "USD", "GBP", "CHF", "JPY", "CAD", "AUD", "SEK", "NOK", "DKK",
        "PLN", "CZK", "HUF", "RON", "HRK", "BGN", "TRY", "CNY", "INR", "BRL",
        "MXN", "ZAR", "AED", "THB", "SGD", "HKD", "NZD"
    };

    private sealed class FrankfurterResponse
    {
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
