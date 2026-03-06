using InvestmentApp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Worker.Jobs;

/// <summary>
/// Refreshes exchange rates once per day at 08:00 VN time.
/// Currently uses fallback seed; swap RefreshRatesAsync for a real
/// HTTP provider (e.g. exchangeratesapi.io) when ready.
/// </summary>
public class ExchangeRateJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ExchangeRateJob> _logger;

    // Run at 08:00 ICT (UTC+7) = 01:00 UTC
    private static readonly TimeOnly _runAt = new(1, 0);

    public ExchangeRateJob(IServiceProvider services, ILogger<ExchangeRateJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExchangeRateJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelay();
            _logger.LogInformation("ExchangeRateJob next run in {Delay:hh\\:mm\\:ss}", delay);
            await Task.Delay(delay, stoppingToken);

            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var currencyService = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
            _logger.LogInformation("Refreshing exchange rates...");
            await currencyService.RefreshRatesAsync(cancellationToken);
            _logger.LogInformation("Exchange rates refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExchangeRateJob failed");
        }
    }

    private static TimeSpan CalculateDelay()
    {
        var now = DateTime.UtcNow;
        var next = DateTime.UtcNow.Date.Add(_runAt.ToTimeSpan());
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
