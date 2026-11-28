using InvestmentApp.Application.Common;
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
    private readonly ICorporateActionRepository _corporateActionRepo;
    private readonly ISignalSeriesWarmup _warmup;
    private readonly ILogger<PriceSnapshotJobService> _logger;
    private readonly TimeSpan _warmupTimeout;

    /// <summary>Hạn của lượt lấy trước chuỗi giá: quá hạn thì bị cắt, job giá không bao giờ chờ nó lâu hơn.</summary>
    public static readonly TimeSpan DefaultWarmupTimeout = TimeSpan.FromSeconds(20);

    private static readonly string[] TrackedIndices = new[] { "VNINDEX", "VN30" };

    public PriceSnapshotJobService(
        ITradeRepository tradeRepo,
        IStockPriceRepository priceRepo,
        IMarketIndexRepository indexRepo,
        IMarketDataProvider marketData,
        IStopLossTargetRepository slRepo,
        ICorporateActionRepository corporateActionRepo,
        ISignalSeriesWarmup warmup,
        ILogger<PriceSnapshotJobService> logger,
        TimeSpan? warmupTimeout = null)
    {
        _tradeRepo = tradeRepo;
        _priceRepo = priceRepo;
        _indexRepo = indexRepo;
        _marketData = marketData;
        _slRepo = slRepo;
        _corporateActionRepo = corporateActionRepo;
        _warmup = warmup;
        _logger = logger;
        _warmupTimeout = warmupTimeout ?? DefaultWarmupTimeout;
    }

    public async Task<PriceSnapshotJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunPricesAsync(cancellationToken);
        await RunWarmupAsync(cancellationToken);   // sau việc chính, kể cả khi không có mã nào để chụp giá
        return result;
    }

    /// <summary>
    /// Lấy trước chuỗi giá đã điều chỉnh của rổ VN30 (tối đa một mã mỗi lượt). Cô lập hoàn toàn: hạn 20 giây, lỗi chỉ được ghi log,
    /// kết quả của job giá không phụ thuộc vào nó. Chỉ huỷ từ chính request mới được ném tiếp.
    /// </summary>
    private async Task RunWarmupAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_warmupTimeout);
        try
        {
            var outcome = await _warmup.WarmNextAsync(cts.Token);
            _logger.LogInformation("Signal warm-up: fetched {Fetched} ({Note})", outcome.Fetched, outcome.Note);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Signal warm-up exceeded {Seconds}s and was cut off", _warmupTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signal warm-up failed; the prices job result is unaffected");
        }
    }

    private async Task<PriceSnapshotJobResult> RunPricesAsync(CancellationToken cancellationToken)
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
        var untriggered = (await _slRepo.GetUntriggeredAsync(cancellationToken)).ToList();

        // Build case-insensitive lookup once
        var priceLookup = new Dictionary<string, StockPriceData>(prices, StringComparer.OrdinalIgnoreCase);

        // Ngưỡng lưu là giá tuyệt đối tại lúc đặt. Sau ngày GDKHQ giá thị trường đã bị
        // điều chỉnh, nên phải điều chỉnh ngưỡng tương ứng — không thì cắt lỗ bắn nhầm.
        var actionLookup = await BuildActionLookupAsync(untriggered, cancellationToken);

        var slCount = 0;
        var targetCount = 0;

        foreach (var slt in untriggered)
        {
            if (!priceLookup.TryGetValue(slt.Symbol, out var price)) continue;

            var currentPrice = price.Close;
            var triggered = false;

            IEnumerable<CorporateAction> actions =
                actionLookup.TryGetValue((slt.PortfolioId, slt.Symbol.ToUpperInvariant()), out var found)
                    ? found
                    : Array.Empty<CorporateAction>();

            var stopLossPrice = CorporateActionAdjuster.AdjustPrice(slt.StopLossPrice, slt.UpdatedAt, actions);
            var targetPrice = CorporateActionAdjuster.AdjustPrice(slt.TargetPrice, slt.UpdatedAt, actions);

            if (stopLossPrice > 0 && currentPrice <= stopLossPrice)
            {
                slt.TriggerStopLoss();
                slCount++;
                triggered = true;
                _logger.LogWarning("Stop-loss triggered for trade {TradeId} at {Price}", slt.TradeId, currentPrice);
            }
            else if (targetPrice > 0 && currentPrice >= targetPrice)
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

    /// <summary>Sự kiện quyền theo (danh mục, mã) cho toàn bộ ngưỡng chưa kích hoạt — một truy vấn duy nhất.</summary>
    private async Task<Dictionary<(string PortfolioId, string Symbol), List<CorporateAction>>> BuildActionLookupAsync(
        IReadOnlyCollection<StopLossTarget> targets, CancellationToken cancellationToken)
    {
        var portfolioIds = targets.Select(t => t.PortfolioId).Distinct().ToList();
        if (portfolioIds.Count == 0)
            return new Dictionary<(string, string), List<CorporateAction>>();

        var actions = await _corporateActionRepo.GetByPortfolioIdsAsync(portfolioIds, cancellationToken);

        return actions
            .GroupBy(a => (a.PortfolioId, a.Symbol.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
