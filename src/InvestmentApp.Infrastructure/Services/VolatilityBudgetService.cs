using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Dựng trần khối lượng theo ngân sách biến động (ADR-0014). Lo phần lấy dữ liệu và bổ khuyết;
/// toàn bộ toán nằm ở <see cref="VolatilityBudgetCalculator"/>.
/// </summary>
public class VolatilityBudgetService : IVolatilityBudgetService
{
    private const string SeriesCacheKeyPrefix = "vol-series:";
    private static readonly TimeSpan SeriesCacheTtl = TimeSpan.FromMinutes(15);
    private const int HistoryWindowDays = 90;

    /// <summary>Mặc định khi danh mục chưa có hồ sơ rủi ro — cùng giá trị <c>RiskProfile</c> dùng.</summary>
    private const decimal DefaultMaxDrawdownPercent = 10m;

    private readonly ITradeRepository _tradeRepository;
    private readonly ICorporateActionRepository _corporateActionRepository;
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly IRiskProfileRepository _riskProfileRepository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VolatilityBudgetService> _logger;

    public VolatilityBudgetService(
        ITradeRepository tradeRepository,
        ICorporateActionRepository corporateActionRepository,
        IStockPriceRepository stockPriceRepository,
        IRiskProfileRepository riskProfileRepository,
        IMarketDataProvider marketDataProvider,
        IMemoryCache cache,
        ILogger<VolatilityBudgetService> logger)
    {
        _tradeRepository = tradeRepository;
        _corporateActionRepository = corporateActionRepository;
        _stockPriceRepository = stockPriceRepository;
        _riskProfileRepository = riskProfileRepository;
        _marketDataProvider = marketDataProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VolatilitySizingResult> GetSizingForPlanAsync(
        string portfolioId, string symbol, decimal entryPrice, int quantity,
        CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpperInvariant().Trim();

        var profile = await _riskProfileRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var maxDrawdown = profile?.MaxDrawdownAlertPercent ?? DefaultMaxDrawdownPercent;

        var result = new VolatilitySizingResult
        {
            Symbol = symbol,
            SourceMaxDrawdownPercent = maxDrawdown,
            BudgetVolatilityPercent = VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(maxDrawdown),
            DataQuality = VolatilityDataQuality.Full
        };

        // Mã đang xét không đủ lịch sử thì không có con số nào dùng được — dừng ngay, không trả
        // một phần kết quả trông như thật.
        var (candidate, candidateFetchFailed) = await GetSeriesAsync(symbol, cancellationToken);
        if (candidate is null)
        {
            result.DataQuality = VolatilityDataQuality.Insufficient;
            if (candidateFetchFailed) result.FetchFailedSymbols.Add(symbol);
            else result.MissingSymbols.Add(symbol);
            return result;
        }
        if (candidate.RemovedCount > 0) result.AdjustedSymbols.Add(symbol);

        var (heldValues, heldSeries, missing, adjusted, fetchFailed) =
            await LoadHoldingsAsync(portfolioId, symbol, cancellationToken);

        result.MissingSymbols.AddRange(missing);
        result.AdjustedSymbols.AddRange(adjusted);
        result.FetchFailedSymbols.AddRange(fetchFailed);
        if (missing.Count > 0 || fetchFailed.Count > 0 || result.AdjustedSymbols.Count > 0)
            result.DataQuality = VolatilityDataQuality.Partial;

        var portfolioValue = heldValues.Sum();
        var portfolioSeries = VolatilityBudgetCalculator.WeightedSeries(heldValues, heldSeries);

        var portfolioVol = VolatilityBudgetCalculator.AnnualizedVolatilityPercent(portfolioSeries);
        var symbolVol = VolatilityBudgetCalculator.AnnualizedVolatilityPercent(candidate.Returns);
        var correlation = portfolioSeries.Count > 1
            ? VolatilityBudgetCalculator.Correlation(candidate.Returns, portfolioSeries)
            : 0m;

        var allocation = entryPrice * quantity;
        var projectedVol = VolatilityBudgetCalculator.ProjectedVolatilityPercent(
            portfolioValue, portfolioVol, allocation, symbolVol, correlation);

        // Số phiên ghép được thật, không phải min hai độ dài: hai chuỗi cùng số phiên vẫn có thể
        // lệch tập ngày, và báo con số dài hơn là nói quá độ tin cậy của ước lượng đang hiện ra.
        result.ObservationCount = portfolioSeries.Count > 0
            ? VolatilityBudgetCalculator.AlignedObservationCount(candidate.Returns, portfolioSeries)
            : candidate.Returns.Count;
        result.CurrentVolatilityPercent = portfolioVol;
        result.ProjectedVolatilityPercent = projectedVol;
        result.CorrelationWithPortfolio = portfolioSeries.Count > 1 ? correlation : null;
        result.PortfolioAlreadyOverBudget =
            portfolioValue > 0m && portfolioVol > result.BudgetVolatilityPercent;

        var maxAllocation = VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue, portfolioVol, symbolVol, correlation, result.BudgetVolatilityPercent);

        if (maxAllocation is null)
        {
            result.IsUnconstrainedByVolatility = true;
        }
        else if (entryPrice > 0m)
        {
            result.MaxQuantityWithinBudget = (int)Math.Floor(maxAllocation.Value / entryPrice);
        }

        var totalAfter = portfolioValue + allocation;
        if (totalAfter > 0m)
        {
            result.CapitalWeightPercent = allocation / totalAfter * 100m;

            var afterSeries = VolatilityBudgetCalculator.WeightedSeries(
                new[] { portfolioValue, allocation },
                new[] { portfolioSeries, candidate.Returns });

            // Hiệp phương sai và phương sai phải cùng đơn vị (lợi suất phiên bình phương) — KHÔNG
            // dùng con số %/năm ở đây.
            var sd = VolatilityBudgetCalculator.StandardDeviation(
                VolatilityBudgetCalculator.Values(afterSeries));
            result.MarginalRiskContributionPercent = VolatilityBudgetCalculator.MarginalRiskContributionPercent(
                allocation / totalAfter,
                VolatilityBudgetCalculator.Covariance(candidate.Returns, afterSeries),
                sd * sd);
        }

        return result;
    }

    /// <summary>Vị thế đang giữ, kèm chuỗi lợi suất. Mã đang xét bị loại nếu đã giữ — nó được cộng
    /// vào ở bước chiếu, cộng hai lần là đếm trùng.</summary>
    private async Task<(List<decimal> Values,
                        List<IReadOnlyList<VolatilityBudgetCalculator.DatedReturn>> Series,
                        List<string> Missing, List<string> Adjusted, List<string> FetchFailed)>
        LoadHoldingsAsync(string portfolioId, string candidateSymbol, CancellationToken cancellationToken)
    {
        var values = new List<decimal>();
        var series = new List<IReadOnlyList<VolatilityBudgetCalculator.DatedReturn>>();
        var missing = new List<string>();
        var adjusted = new List<string>();
        var fetchFailed = new List<string>();

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var actions = await _corporateActionRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);

        // Qua PositionBuilder — nguồn duy nhất của toán vị thế (ADR-0010).
        var positions = PositionBuilder.Build(trades, actions, DateTime.UtcNow)
            .Where(p => p.TotalQuantity > 0
                        && !string.Equals(p.Symbol, candidateSymbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var position in positions)
        {
            var (held, heldFetchFailed) = await GetSeriesAsync(position.Symbol, cancellationToken);
            if (held is null)
            {
                if (heldFetchFailed) fetchFailed.Add(position.Symbol);
                else missing.Add(position.Symbol);
                continue;
            }
            if (held.RemovedCount > 0) adjusted.Add(position.Symbol);

            // Giá đóng cửa cuối của chính chuỗi vừa lấy — không thêm một lần gọi giá nào, và trọng
            // số nằm cùng mặt bằng giá với lợi suất dùng để ước lượng.
            values.Add(position.TotalQuantity * held.LastClose);
            series.Add(held.Returns);
        }

        return (values, series, missing, adjusted, fetchFailed);
    }

    private sealed record SymbolSeries(
        IReadOnlyList<VolatilityBudgetCalculator.DatedReturn> Returns, decimal LastClose, int RemovedCount);

    /// <summary>
    /// Chuỗi lợi suất đã lọc cho một mã. Đọc kho cục bộ trước; thiếu thì lấy theo phiên qua provider
    /// rồi ghi lại để lần sau khỏi gọi. Trả <c>null</c> khi vẫn không đủ quan sát — không trả chuỗi
    /// ngắn kèm một con số kém tin.
    /// </summary>
    /// <param name="FetchFailed">
    /// <c>true</c> khi việc LẤY lịch sử hỏng, phân biệt với mã thật sự chưa đủ lịch sử. Hai ca đều
    /// cho <c>Series = null</c> nhưng câu nói với người dùng phải khác nhau.
    /// </param>
    private async Task<(SymbolSeries? Series, bool FetchFailed)> GetSeriesAsync(
        string symbol, CancellationToken cancellationToken)
    {
        var cacheKey = SeriesCacheKeyPrefix + symbol.ToUpperInvariant();
        if (_cache.TryGetValue<SymbolSeries>(cacheKey, out var cached))
            return (cached, false);

        var closes = await ReadLocalClosesAsync(symbol, cancellationToken);

        var fetchFailed = false;
        if (closes.Count <= VolatilityBudgetCalculator.MinimumObservations)
        {
            fetchFailed = !await BackfillAsync(symbol, cancellationToken);
            closes = await ReadLocalClosesAsync(symbol, cancellationToken);
        }

        var raw = VolatilityBudgetCalculator.ToReturns(closes);
        var (kept, removed) = VolatilityBudgetCalculator.FilterAbnormalReturns(raw);

        if (kept.Count < VolatilityBudgetCalculator.MinimumObservations)
            return (null, fetchFailed);

        // Lấy hỏng nhưng kho cục bộ vẫn đủ dùng thì đây không phải sự cố người dùng cần biết.
        var result = new SymbolSeries(kept, closes[^1].Close, removed);
        // Chỉ đệm khi ĐỦ dữ liệu. Đệm ca thiếu là đóng băng mã đó thành "không tính được" suốt TTL,
        // tức một lỗi mạng nhất thời làm im panel trong 15 phút.
        _cache.Set(cacheKey, result, SeriesCacheTtl);
        return (result, false);
    }

    private async Task<List<(DateTime Date, decimal Close)>> ReadLocalClosesAsync(
        string symbol, CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.Date.AddDays(-HistoryWindowDays);
        var to = DateTime.UtcNow.Date.AddDays(1);
        var prices = await _stockPriceRepository.GetBySymbolAsync(symbol, from, to, cancellationToken);

        return prices
            .Where(p => p.Close > 0)
            .GroupBy(p => p.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.OrderByDescending(p => p.Date).First().Close))
            .ToList();
    }

    /// <returns><c>false</c> khi lấy lịch sử hỏng — người gọi phải phân biệt với "mã không có dữ liệu".</returns>
    private async Task<bool> BackfillAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _marketDataProvider.GetDailyHistoryAsync(symbol, cancellationToken);
            foreach (var bar in history)
            {
                await _stockPriceRepository.UpsertAsync(
                    new Domain.Entities.StockPrice(
                        symbol, bar.Date, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume,
                        "VolatilityBudgetBackfill"),
                    cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Không để thành lỗi 500 cho một panel chỉ mang tính tham khảo — nhưng cũng không im.
            // Trả false để giao diện nói "chưa lấy được lịch sử giá" thay vì "chưa đủ lịch sử giá":
            // câu sau là sai sự thật khi mã đó thật ra có thừa dữ liệu.
            _logger.LogWarning(ex, "Failed to backfill daily history for {Symbol}", symbol);
            return false;
        }
    }
}
