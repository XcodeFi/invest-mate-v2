using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

public class PriceSnapshotJobService : IPriceSnapshotJobService
{
    private readonly ITradeRepository _tradeRepo;
    private readonly IStockPriceRepository _priceRepo;
    private readonly IMarketIndexRepository _indexRepo;
    private readonly IMarketDataProvider _marketData;
    private readonly IStopLossTargetRepository _slRepo;
    private readonly ILogger<PriceSnapshotJobService> _logger;

    private static readonly string[] TrackedIndices = new[] { "VNINDEX", "VN30" };

    public PriceSnapshotJobService(
        ITradeRepository tradeRepo,
        IStockPriceRepository priceRepo,
        IMarketIndexRepository indexRepo,
        IMarketDataProvider marketData,
        IStopLossTargetRepository slRepo,
        ILogger<PriceSnapshotJobService> logger)
    {
        _tradeRepo = tradeRepo;
        _priceRepo = priceRepo;
        _indexRepo = indexRepo;
        _marketData = marketData;
        _slRepo = slRepo;
        _logger = logger;
    }

    public async Task<PriceSnapshotJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var allTrades = await _tradeRepo.GetAllAsync(cancellationToken);
        var symbols = allTrades
            .Select(t => t.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbols.Count == 0)
        {
            _logger.LogDebug("No symbols found, skipping price snapshot");
            return PriceSnapshotJobResult.Empty;
        }

        _logger.LogInformation("Fetching prices for {Count} symbols: {Symbols}",
            symbols.Count, string.Join(", ", symbols.Take(10)));

        var prices = await _marketData.GetBatchPricesAsync(symbols, cancellationToken);

        var pricesPersisted = 0;
        foreach (var (symbol, data) in prices)
        {
            var stockPrice = new StockPrice(
                symbol, data.Date,
                data.Open, data.High, data.Low, data.Close,
                data.Volume, "PriceSnapshotJob");
            await _priceRepo.UpsertAsync(stockPrice, cancellationToken);
            pricesPersisted++;
        }

        _logger.LogInformation("Persisted {Count} price snapshots", pricesPersisted);

        var indicesUpdated = await UpdateMarketIndicesAsync(cancellationToken);
        var (slTriggered, targetTriggered) = await CheckStopLossTriggersAsync(prices, cancellationToken);

        return new PriceSnapshotJobResult(
            SymbolsFetched: symbols.Count,
            PricesPersisted: pricesPersisted,
            IndicesUpdated: indicesUpdated,
            StopLossTriggered: slTriggered,
            TargetsTriggered: targetTriggered);
    }

    private async Task<int> UpdateMarketIndicesAsync(CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var indexSymbol in TrackedIndices)
        {
            var indexData = await _marketData.GetIndexDataAsync(indexSymbol, cancellationToken);
            if (indexData == null) continue;

            var marketIndex = new MarketIndex(
                indexData.IndexSymbol, indexData.Date,
                indexData.PriorClose, indexData.High, indexData.Low, indexData.Close,
                indexData.Volume);
            await _indexRepo.UpsertAsync(marketIndex, cancellationToken);
            updated++;
        }
        return updated;
    }

    private async Task<(int StopLossTriggered, int TargetsTriggered)> CheckStopLossTriggersAsync(
        Dictionary<string, StockPriceData> prices,
        CancellationToken cancellationToken)
    {
        var untriggered = await _slRepo.GetUntriggeredAsync(cancellationToken);

        // Build case-insensitive lookup once
        var priceLookup = new Dictionary<string, StockPriceData>(prices, StringComparer.OrdinalIgnoreCase);

        var slCount = 0;
        var targetCount = 0;

        foreach (var slt in untriggered)
        {
            if (!priceLookup.TryGetValue(slt.Symbol, out var price)) continue;

            var currentPrice = price.Close;
            var triggered = false;

            if (slt.StopLossPrice > 0 && currentPrice <= slt.StopLossPrice)
            {
                slt.TriggerStopLoss();
                slCount++;
                triggered = true;
                _logger.LogWarning("Stop-loss triggered for trade {TradeId} at {Price}", slt.TradeId, currentPrice);
            }
            else if (slt.TargetPrice > 0 && currentPrice >= slt.TargetPrice)
            {
                slt.TriggerTarget();
                targetCount++;
                triggered = true;
                _logger.LogInformation("Target price hit for trade {TradeId} at {Price}", slt.TradeId, currentPrice);
            }

            if (triggered)
                await _slRepo.UpdateAsync(slt, cancellationToken);
        }

        return (slCount, targetCount);
    }
}
