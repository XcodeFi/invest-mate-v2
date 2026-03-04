using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Service for calculating advanced performance metrics (quant-level analytics).
/// Uses snapshot data for time-series calculations and trade data for win/loss analysis.
/// </summary>
public class PerformanceMetricsService : IPerformanceMetricsService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IPnLService _pnlService;
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly ILogger<PerformanceMetricsService> _logger;

    public PerformanceMetricsService(
        IPortfolioRepository portfolioRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        ITradeRepository tradeRepository,
        ICapitalFlowRepository capitalFlowRepository,
        IPnLService pnlService,
        IRiskCalculationService riskCalculationService,
        ILogger<PerformanceMetricsService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _snapshotRepository = snapshotRepository;
        _tradeRepository = tradeRepository;
        _capitalFlowRepository = capitalFlowRepository;
        _pnlService = pnlService;
        _riskCalculationService = riskCalculationService;
        _logger = logger;
    }

    public async Task<decimal> CalculateCAGRAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null) throw new ArgumentException("Portfolio not found");

        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.MinValue, DateTime.UtcNow, cancellationToken);
        var sorted = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        if (sorted.Count < 2) return 0;

        var startValue = sorted.First().TotalValue;
        var endValue = sorted.Last().TotalValue;
        if (startValue <= 0) return 0;

        var years = (sorted.Last().SnapshotDate - sorted.First().SnapshotDate).TotalDays / 365.25;
        if (years < 0.01) return 0;

        // CAGR = (EndValue / StartValue)^(1/Years) - 1
        var ratio = (double)(endValue / startValue);
        var cagr = (decimal)(Math.Pow(ratio, 1.0 / years) - 1) * 100;
        return Math.Round(cagr, 2);
    }

    public async Task<decimal> CalculateSharpeRatioAsync(string portfolioId, decimal riskFreeRate = 0.05m, CancellationToken cancellationToken = default)
    {
        var dailyReturns = await GetDailyReturnsAsync(portfolioId, cancellationToken);
        if (dailyReturns.Count < 10) return 0;

        var avgReturn = dailyReturns.Average();
        var dailyRf = riskFreeRate / 252m; // Annualized to daily

        var variance = dailyReturns.Sum(r => (r - avgReturn) * (r - avgReturn)) / dailyReturns.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        if (stdDev == 0) return 0;

        // Sharpe = (Rp - Rf) / σp, annualized
        var sharpe = (avgReturn - dailyRf) / stdDev * (decimal)Math.Sqrt(252.0);
        return Math.Round(sharpe, 2);
    }

    public async Task<decimal> CalculateSortinoRatioAsync(string portfolioId, decimal riskFreeRate = 0.05m, CancellationToken cancellationToken = default)
    {
        var dailyReturns = await GetDailyReturnsAsync(portfolioId, cancellationToken);
        if (dailyReturns.Count < 10) return 0;

        var avgReturn = dailyReturns.Average();
        var dailyRf = riskFreeRate / 252m;

        // Downside deviation: only negative returns
        var downsideReturns = dailyReturns.Where(r => r < dailyRf).ToList();
        if (downsideReturns.Count == 0) return 0;

        var downsideVariance = downsideReturns.Sum(r => (r - dailyRf) * (r - dailyRf)) / downsideReturns.Count;
        var downsideDeviation = (decimal)Math.Sqrt((double)downsideVariance);

        if (downsideDeviation == 0) return 0;

        // Sortino = (Rp - Rf) / σd, annualized
        var sortino = (avgReturn - dailyRf) / downsideDeviation * (decimal)Math.Sqrt(252.0);
        return Math.Round(sortino, 2);
    }

    public async Task<decimal> CalculateWinRateAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var (wins, losses, _, _, _, _) = await AnalyzeTradesAsync(portfolioId, cancellationToken);
        var total = wins + losses;
        if (total == 0) return 0;
        return Math.Round((decimal)wins / total * 100, 2);
    }

    public async Task<decimal> CalculateProfitFactorAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var (_, _, grossProfit, grossLoss, _, _) = await AnalyzeTradesAsync(portfolioId, cancellationToken);
        if (grossLoss == 0) return grossProfit > 0 ? 999.99m : 0;
        return Math.Round(grossProfit / Math.Abs(grossLoss), 2);
    }

    public async Task<decimal> CalculateExpectancyAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var (wins, losses, _, _, avgWin, avgLoss) = await AnalyzeTradesAsync(portfolioId, cancellationToken);
        var total = wins + losses;
        if (total == 0) return 0;

        var winRate = (decimal)wins / total;
        var lossRate = (decimal)losses / total;
        // Expectancy = (Win% × Avg Win) - (Loss% × Avg Loss)
        return Math.Round(winRate * avgWin - lossRate * Math.Abs(avgLoss), 2);
    }

    public async Task<PerformanceSummary> GetFullPerformanceSummaryAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var (wins, losses, grossProfit, grossLoss, avgWin, avgLoss) = await AnalyzeTradesAsync(portfolioId, cancellationToken);
        var drawdown = await _riskCalculationService.CalculateMaxDrawdownAsync(portfolioId, cancellationToken);

        decimal cagr = 0, sharpe = 0, sortino = 0;
        try { cagr = await CalculateCAGRAsync(portfolioId, cancellationToken); } catch { }
        try { sharpe = await CalculateSharpeRatioAsync(portfolioId, 0.05m, cancellationToken); } catch { }
        try { sortino = await CalculateSortinoRatioAsync(portfolioId, 0.05m, cancellationToken); } catch { }

        var total = wins + losses;
        var winRate = total > 0 ? Math.Round((decimal)wins / total * 100, 2) : 0;
        var profitFactor = grossLoss != 0 ? Math.Round(grossProfit / Math.Abs(grossLoss), 2) : 0;
        var expectancy = total > 0 ? Math.Round((decimal)wins / total * avgWin - (decimal)losses / total * Math.Abs(avgLoss), 2) : 0;

        // Total return from snapshots
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.MinValue, DateTime.UtcNow, cancellationToken);
        var sorted = snapshots.OrderBy(s => s.SnapshotDate).ToList();
        decimal totalReturn = 0;
        if (sorted.Count >= 2 && sorted.First().TotalValue > 0)
        {
            totalReturn = ((sorted.Last().TotalValue - sorted.First().TotalValue) / sorted.First().TotalValue) * 100;
        }

        return new PerformanceSummary
        {
            PortfolioId = portfolioId,
            CAGR = cagr,
            SharpeRatio = sharpe,
            SortinoRatio = sortino,
            WinRate = winRate,
            ProfitFactor = profitFactor,
            Expectancy = expectancy,
            MaxDrawdown = drawdown.MaxDrawdownPercent,
            TotalReturn = Math.Round(totalReturn, 2),
            TotalTrades = total,
            WinningTrades = wins,
            LosingTrades = losses,
            AverageWin = avgWin,
            AverageLoss = avgLoss,
            GrossProfit = grossProfit,
            GrossLoss = grossLoss
        };
    }

    public async Task<EquityCurveData> GetEquityCurveAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.MinValue, DateTime.UtcNow, cancellationToken);
        var sorted = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        return new EquityCurveData
        {
            PortfolioId = portfolioId,
            Points = sorted.Select(s => new EquityCurvePoint
            {
                Date = s.SnapshotDate,
                PortfolioValue = s.TotalValue,
                DailyReturn = s.DailyReturn,
                CumulativeReturn = s.CumulativeReturn
            }).ToList()
        };
    }

    public async Task<MonthlyReturnsData> GetMonthlyReturnsAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.MinValue, DateTime.UtcNow, cancellationToken);
        var sorted = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        if (sorted.Count < 2)
            return new MonthlyReturnsData { PortfolioId = portfolioId };

        // Group by month and calculate monthly returns
        var monthlyGroups = sorted
            .GroupBy(s => new { s.SnapshotDate.Year, s.SnapshotDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .ToList();

        var monthlyReturns = new List<MonthlyReturnItem>();
        for (int i = 1; i < monthlyGroups.Count; i++)
        {
            var prevMonth = monthlyGroups[i - 1].OrderByDescending(s => s.SnapshotDate).First();
            var currMonth = monthlyGroups[i].OrderByDescending(s => s.SnapshotDate).First();

            var monthReturn = prevMonth.TotalValue > 0
                ? ((currMonth.TotalValue - prevMonth.TotalValue) / prevMonth.TotalValue) * 100
                : 0;

            monthlyReturns.Add(new MonthlyReturnItem
            {
                Year = monthlyGroups[i].Key.Year,
                Month = monthlyGroups[i].Key.Month,
                ReturnPercent = Math.Round(monthReturn, 2)
            });
        }

        return new MonthlyReturnsData
        {
            PortfolioId = portfolioId,
            Returns = monthlyReturns,
            Years = monthlyReturns.Select(r => r.Year).Distinct().OrderBy(y => y).ToList()
        };
    }

    /// <summary>
    /// Analyze completed sell trades to determine win/loss statistics.
    /// </summary>
    private async Task<(int wins, int losses, decimal grossProfit, decimal grossLoss, decimal avgWin, decimal avgLoss)> AnalyzeTradesAsync(
        string portfolioId, CancellationToken cancellationToken)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var tradeList = trades.ToList();

        // Group by symbol and analyze sell trades vs average cost
        var tradesBySymbol = tradeList.GroupBy(t => t.Symbol);
        var tradePnLs = new List<decimal>();

        foreach (var symbolGroup in tradesBySymbol)
        {
            var buys = symbolGroup.Where(t => t.TradeType == TradeType.BUY).OrderBy(t => t.TradeDate).ToList();
            var sells = symbolGroup.Where(t => t.TradeType == TradeType.SELL).OrderBy(t => t.TradeDate).ToList();

            if (buys.Count == 0 || sells.Count == 0) continue;

            // Calculate average cost
            var totalBuyQty = buys.Sum(b => b.Quantity);
            var totalBuyCost = buys.Sum(b => b.Quantity * b.Price);
            var avgCost = totalBuyQty > 0 ? totalBuyCost / totalBuyQty : 0;

            // Each sell is a "trade result"
            foreach (var sell in sells)
            {
                var pnl = sell.Quantity * (sell.Price - avgCost) - sell.Fee - sell.Tax;
                tradePnLs.Add(pnl);
            }
        }

        if (tradePnLs.Count == 0)
            return (0, 0, 0, 0, 0, 0);

        var winTrades = tradePnLs.Where(p => p > 0).ToList();
        var lossTrades = tradePnLs.Where(p => p <= 0).ToList();

        return (
            wins: winTrades.Count,
            losses: lossTrades.Count,
            grossProfit: winTrades.Sum(),
            grossLoss: lossTrades.Sum(),
            avgWin: winTrades.Count > 0 ? Math.Round(winTrades.Average(), 2) : 0,
            avgLoss: lossTrades.Count > 0 ? Math.Round(lossTrades.Average(), 2) : 0
        );
    }

    /// <summary>
    /// Get daily return series from snapshots.
    /// </summary>
    private async Task<List<decimal>> GetDailyReturnsAsync(string portfolioId, CancellationToken cancellationToken)
    {
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, cancellationToken);
        var sorted = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        var returns = new List<decimal>();
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i - 1].TotalValue > 0)
            {
                returns.Add((sorted[i].TotalValue - sorted[i - 1].TotalValue) / sorted[i - 1].TotalValue);
            }
        }
        return returns;
    }
}
