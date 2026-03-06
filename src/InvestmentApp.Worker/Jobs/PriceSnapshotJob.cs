using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Worker.Jobs;

/// <summary>
/// Runs every 15 minutes during VN market hours (09:00–15:00 ICT = 02:00–08:00 UTC).
/// 1. Collects all unique symbols from active portfolio trades
/// 2. Batch fetches current prices via IMarketDataProvider
/// 3. Persists to stock_prices collection (upsert)
/// 4. Refreshes VNINDEX market index
/// 5. Checks stop-loss / target triggers
/// </summary>
public class PriceSnapshotJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PriceSnapshotJob> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    // VN market hours in UTC (ICT = UTC+7)
    private static readonly TimeOnly _marketOpen = new(2, 0);   // 09:00 ICT
    private static readonly TimeOnly _marketClose = new(8, 0);  // 15:00 ICT

    public PriceSnapshotJob(IServiceProvider services, ILogger<PriceSnapshotJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceSnapshotJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (IsMarketOpen())
            {
                await RunAsync(stoppingToken);
            }
            else
            {
                _logger.LogDebug("Market closed, PriceSnapshotJob skipping run");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var tradeRepo = scope.ServiceProvider.GetRequiredService<ITradeRepository>();
            var priceRepo = scope.ServiceProvider.GetRequiredService<IStockPriceRepository>();
            var indexRepo = scope.ServiceProvider.GetRequiredService<IMarketIndexRepository>();
            var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
            var slRepo = scope.ServiceProvider.GetRequiredService<IStopLossTargetRepository>();

            // 1. Collect all unique symbols from trades
            var allTrades = await tradeRepo.GetAllAsync(cancellationToken);
            var symbols = allTrades
                .Select(t => t.Symbol)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!symbols.Any())
            {
                _logger.LogDebug("No symbols found, skipping price snapshot");
                return;
            }

            _logger.LogInformation("Fetching prices for {Count} symbols: {Symbols}",
                symbols.Count, string.Join(", ", symbols.Take(10)));

            // 2. Batch fetch prices
            var prices = await marketData.GetBatchPricesAsync(symbols, cancellationToken);

            // 3. Persist to DB
            foreach (var (symbol, data) in prices)
            {
                var stockPrice = new StockPrice(
                    symbol, data.Date,
                    data.Open, data.High, data.Low, data.Close,
                    data.Volume, "PriceSnapshotJob");
                await priceRepo.UpsertAsync(stockPrice, cancellationToken);
            }

            _logger.LogInformation("Persisted {Count} price snapshots", prices.Count);

            // 4. Refresh VNINDEX
            foreach (var indexSymbol in new[] { "VNINDEX", "VN30" })
            {
                var indexData = await marketData.GetIndexDataAsync(indexSymbol, cancellationToken);
                if (indexData == null) continue;

                var marketIndex = new MarketIndex(
                    indexData.IndexSymbol, indexData.Date,
                    indexData.Open, indexData.High, indexData.Low, indexData.Close,
                    indexData.Volume);
                await indexRepo.UpsertAsync(marketIndex, cancellationToken);
            }

            // 5. Check stop-loss / target triggers
            await CheckStopLossTriggersAsync(slRepo, prices, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PriceSnapshotJob run failed");
        }
    }

    private async Task CheckStopLossTriggersAsync(
        IStopLossTargetRepository slRepo,
        Dictionary<string, StockPriceData> prices,
        CancellationToken cancellationToken)
    {
        var untriggered = await slRepo.GetUntriggeredAsync(cancellationToken);

        foreach (var slt in untriggered)
        {
            if (!prices.TryGetValue(slt.Symbol.ToUpperInvariant(), out var price)) continue;

            var currentPrice = price.Close;
            bool triggered = false;

            if (slt.StopLossPrice > 0 && currentPrice <= slt.StopLossPrice)
            {
                slt.TriggerStopLoss();
                triggered = true;
                _logger.LogWarning("Stop-loss triggered for trade {TradeId} at {Price}", slt.TradeId, currentPrice);
            }
            else if (slt.TargetPrice > 0 && currentPrice >= slt.TargetPrice)
            {
                slt.TriggerTarget();
                triggered = true;
                _logger.LogInformation("Target price hit for trade {TradeId} at {Price}", slt.TradeId, currentPrice);
            }

            if (triggered)
                await slRepo.UpdateAsync(slt, cancellationToken);
        }
    }

    private static bool IsMarketOpen()
    {
        var nowUtc = TimeOnly.FromDateTime(DateTime.UtcNow);
        var dayOfWeek = DateTime.UtcNow.DayOfWeek;
        if (dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return nowUtc >= _marketOpen && nowUtc <= _marketClose;
    }
}
