using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Services;

/// <summary>
/// Holt die Bankumsaetze regelmaessig nach. Weil der Import ueber die Bank-Referenz
/// dedupliziert, waechst die lokale Datenbank monoton - auch ueber die 90 Tage hinaus,
/// die die Bank je Abruf liefert.
/// </summary>
public class BankSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BankSyncWorker> _log;

    public BankSyncWorker(IServiceScopeFactory scopes, ILogger<BankSyncWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stunden = double.TryParse(Environment.GetEnvironmentVariable("BANK_SYNC_HOURS"), out var h) && h > 0 ? h : 6;
        var abstand = TimeSpan.FromHours(stunden);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(abstand, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var importer = scope.ServiceProvider.GetRequiredService<BankImportService>();
                var ergebnis = await importer.ImportAsync(stoppingToken);
                _log.LogInformation("Bankabgleich: {Summary}", ergebnis.Summary);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Bankabgleich fehlgeschlagen");
            }
        }
    }
}
