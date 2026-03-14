using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Service for calculating position-level and portfolio-level risk metrics.
/// </summary>
public class RiskCalculationService : IRiskCalculationService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IStockPriceService _stockPriceService;
    private readonly IStopLossTargetRepository _stopLossTargetRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly IPnLService _pnlService;
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly ILogger<RiskCalculationService> _logger;

    public RiskCalculationService(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        IStockPriceService stockPriceService,
        IStopLossTargetRepository stopLossTargetRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IStockPriceRepository stockPriceRepository,
        IPnLService pnlService,
        ICapitalFlowRepository capitalFlowRepository,
        ILogger<RiskCalculationService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _stockPriceService = stockPriceService;
        _stopLossTargetRepository = stopLossTargetRepository;
        _snapshotRepository = snapshotRepository;
        _stockPriceRepository = stockPriceRepository;
        _pnlService = pnlService;
        _capitalFlowRepository = capitalFlowRepository;
        _logger = logger;
    }

    public async Task<decimal> CalculatePositionSizePercentAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        if (pnlSummary.TotalPortfolioValue <= 0) return 0;

        try
        {
            var positionPnl = await _pnlService.CalculatePositionPnLAsync(portfolioId, new StockSymbol(symbol), cancellationToken);
            return (positionPnl.MarketValue / pnlSummary.TotalPortfolioValue) * 100;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<PortfolioRiskSummary> GetPortfolioRiskSummaryAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var tradesBySymbol = trades.GroupBy(t => t.Symbol).ToList();
        var stopLossTargets = await _stopLossTargetRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var slTargetMap = stopLossTargets
            .GroupBy(s => s.Symbol)
            .ToDictionary(g => g.Key, g => g.Last());

        // Calculate total portfolio value
        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        var totalFlows = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolioId, cancellationToken);
        var cashBalance = portfolio.InitialCapital + totalFlows - pnlSummary.TotalInvested;
        var totalValue = pnlSummary.TotalPortfolioValue + cashBalance;

        var positions = new List<PositionRiskItem>();
        foreach (var symbolGroup in tradesBySymbol)
        {
            try
            {
                var positionPnl = await _pnlService.CalculatePositionPnLAsync(
                    portfolioId, new StockSymbol(symbolGroup.Key), cancellationToken);

                if (positionPnl.Quantity <= 0) continue;

                var positionSizePercent = totalValue > 0 ? (positionPnl.MarketValue / totalValue) * 100 : 0;
                slTargetMap.TryGetValue(symbolGroup.Key, out var slTarget);

                var item = new PositionRiskItem
                {
                    Symbol = symbolGroup.Key,
                    Quantity = positionPnl.Quantity,
                    CurrentPrice = positionPnl.CurrentPrice,
                    MarketValue = positionPnl.MarketValue,
                    PositionSizePercent = positionSizePercent,
                    StopLossPrice = slTarget?.StopLossPrice,
                    TargetPrice = slTarget?.TargetPrice,
                    RiskRewardRatio = slTarget?.GetRiskRewardRatio(),
                    RiskPerShare = slTarget?.GetRiskPerShare(),
                    RiskAmount = slTarget != null ? slTarget.GetRiskPerShare() * positionPnl.Quantity : null,
                    DistanceToStopLossPercent = slTarget != null && positionPnl.CurrentPrice > 0
                        ? ((positionPnl.CurrentPrice - slTarget.StopLossPrice) / positionPnl.CurrentPrice) * 100 : 0,
                    DistanceToTargetPercent = slTarget != null && positionPnl.CurrentPrice > 0
                        ? ((slTarget.TargetPrice - positionPnl.CurrentPrice) / positionPnl.CurrentPrice) * 100 : 0
                };
                positions.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating risk for {Symbol} in portfolio {PortfolioId}",
                    symbolGroup.Key, portfolioId);
            }
        }

        // Get drawdown and VaR
        var drawdown = await CalculateMaxDrawdownAsync(portfolioId, cancellationToken);
        decimal var95 = 0;
        try { var95 = await CalculateValueAtRiskAsync(portfolioId, cancellationToken); } catch { }

        return new PortfolioRiskSummary
        {
            PortfolioId = portfolioId,
            TotalValue = totalValue,
            Positions = positions,
            MaxDrawdown = drawdown.MaxDrawdownPercent,
            ValueAtRisk95 = var95,
            LargestPositionPercent = positions.Any() ? positions.Max(p => p.PositionSizePercent) : 0,
            PositionCount = positions.Count
        };
    }

    public async Task<DrawdownResult> CalculateMaxDrawdownAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.UtcNow.AddYears(-5), DateTime.UtcNow, cancellationToken);
        var sortedSnapshots = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        var result = new DrawdownResult { PortfolioId = portfolioId };

        if (sortedSnapshots.Count < 2) return result;

        decimal peak = sortedSnapshots[0].TotalValue;
        DateTime peakDate = sortedSnapshots[0].SnapshotDate;
        decimal maxDrawdown = 0;
        DateTime? maxDrawdownPeakDate = null;
        decimal? maxDrawdownPeakValue = null;
        DateTime? maxDrawdownTroughDate = null;
        decimal? maxDrawdownTroughValue = null;

        foreach (var snapshot in sortedSnapshots)
        {
            if (snapshot.TotalValue > peak)
            {
                peak = snapshot.TotalValue;
                peakDate = snapshot.SnapshotDate;
            }

            var drawdown = peak > 0 ? ((peak - snapshot.TotalValue) / peak) * 100 : 0;
            result.DrawdownSeries.Add(new DrawdownPoint
            {
                Date = snapshot.SnapshotDate,
                Value = snapshot.TotalValue,
                DrawdownPercent = drawdown
            });

            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
                maxDrawdownPeakDate = peakDate;
                maxDrawdownPeakValue = peak;
                maxDrawdownTroughDate = snapshot.SnapshotDate;
                maxDrawdownTroughValue = snapshot.TotalValue;
            }
        }

        // Current drawdown
        var lastSnapshot = sortedSnapshots.Last();
        var currentDrawdown = peak > 0 ? ((peak - lastSnapshot.TotalValue) / peak) * 100 : 0;

        result.MaxDrawdownPercent = maxDrawdown;
        result.CurrentDrawdownPercent = currentDrawdown;
        result.PeakDate = maxDrawdownPeakDate;
        result.PeakValue = maxDrawdownPeakValue;
        result.TroughDate = maxDrawdownTroughDate;
        result.TroughValue = maxDrawdownTroughValue;

        return result;
    }

    public async Task<decimal> CalculateValueAtRiskAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        // VaR (95%) = μ - 1.645σ (parametric method)
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, cancellationToken);
        var sortedSnapshots = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        if (sortedSnapshots.Count < 10) return 0;

        // Calculate daily returns
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < sortedSnapshots.Count; i++)
        {
            if (sortedSnapshots[i - 1].TotalValue > 0)
            {
                var dailyReturn = (sortedSnapshots[i].TotalValue - sortedSnapshots[i - 1].TotalValue) / sortedSnapshots[i - 1].TotalValue;
                dailyReturns.Add(dailyReturn);
            }
        }

        if (dailyReturns.Count < 5) return 0;

        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / dailyReturns.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        // VaR at 95% confidence = μ - 1.645σ (as percentage)
        var var95 = (mean - 1.645m * stdDev) * 100;
        return Math.Abs(var95);
    }

    public async Task<CorrelationMatrix> CalculateCorrelationMatrixAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var symbols = trades.Select(t => t.Symbol).Distinct().ToList();

        var result = new CorrelationMatrix
        {
            PortfolioId = portfolioId,
            Symbols = symbols
        };

        if (symbols.Count < 2) return result;

        // Get price history for each symbol (last 90 days)
        var from = DateTime.UtcNow.AddDays(-90);
        var to = DateTime.UtcNow;
        var priceHistories = new Dictionary<string, List<decimal>>();

        foreach (var symbol in symbols)
        {
            var prices = await _stockPriceRepository.GetBySymbolAsync(symbol, from, to, cancellationToken);
            var priceList = prices.OrderBy(p => p.Date).Select(p => p.Close).ToList();
            if (priceList.Count > 1)
            {
                // Convert to returns
                var returns = new List<decimal>();
                for (int i = 1; i < priceList.Count; i++)
                {
                    if (priceList[i - 1] > 0)
                        returns.Add((priceList[i] - priceList[i - 1]) / priceList[i - 1]);
                }
                priceHistories[symbol] = returns;
            }
        }

        // Calculate correlation pairs
        var pairs = new List<CorrelationPair>();
        for (int i = 0; i < symbols.Count; i++)
        {
            for (int j = i + 1; j < symbols.Count; j++)
            {
                if (priceHistories.TryGetValue(symbols[i], out var returns1) &&
                    priceHistories.TryGetValue(symbols[j], out var returns2))
                {
                    var correlation = CalculateCorrelation(returns1, returns2);
                    pairs.Add(new CorrelationPair
                    {
                        Symbol1 = symbols[i],
                        Symbol2 = symbols[j],
                        Correlation = correlation
                    });
                }
            }
        }

        result.Pairs = pairs;
        return result;
    }

    private static decimal CalculateCorrelation(List<decimal> x, List<decimal> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 5) return 0;

        var xSubset = x.Take(n).ToList();
        var ySubset = y.Take(n).ToList();

        var meanX = xSubset.Average();
        var meanY = ySubset.Average();

        decimal covXY = 0, varX = 0, varY = 0;
        for (int i = 0; i < n; i++)
        {
            var dx = xSubset[i] - meanX;
            var dy = ySubset[i] - meanY;
            covXY += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        if (varX == 0 || varY == 0) return 0;
        return covXY / ((decimal)Math.Sqrt((double)(varX * varY)));
    }
}
