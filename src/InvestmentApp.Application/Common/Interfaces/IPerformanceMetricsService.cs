namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Service for calculating advanced performance metrics (quant-level analytics).
/// </summary>
public interface IPerformanceMetricsService
{
    /// <summary>
    /// Calculates Compound Annual Growth Rate.
    /// CAGR = (End Value / Start Value)^(1/Years) - 1
    /// </summary>
    Task<decimal> CalculateCAGRAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Sharpe Ratio = (Rp - Rf) / σp
    /// </summary>
    Task<decimal> CalculateSharpeRatioAsync(string portfolioId, decimal riskFreeRate = 0.05m, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Sortino Ratio = (Rp - Rf) / σd (downside deviation only)
    /// </summary>
    Task<decimal> CalculateSortinoRatioAsync(string portfolioId, decimal riskFreeRate = 0.05m, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Win Rate = Winning Trades / Total Trades × 100
    /// </summary>
    Task<decimal> CalculateWinRateAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Profit Factor = Gross Profit / Gross Loss
    /// </summary>
    Task<decimal> CalculateProfitFactorAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Expectancy = (Win% × Avg Win) - (Loss% × Avg Loss)
    /// </summary>
    Task<decimal> CalculateExpectancyAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full performance summary with all metrics.
    /// </summary>
    Task<PerformanceSummary> GetFullPerformanceSummaryAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets equity curve data points from snapshots.
    /// </summary>
    Task<EquityCurveData> GetEquityCurveAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets monthly returns heatmap data.
    /// </summary>
    Task<MonthlyReturnsData> GetMonthlyReturnsAsync(string portfolioId, CancellationToken cancellationToken = default);
}

public class PerformanceSummary
{
    public string PortfolioId { get; set; } = null!;
    public decimal CAGR { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal TotalReturn { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
}

public class EquityCurveData
{
    public string PortfolioId { get; set; } = null!;
    public List<EquityCurvePoint> Points { get; set; } = new();
}

public class EquityCurvePoint
{
    public DateTime Date { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal DailyReturn { get; set; }
    public decimal CumulativeReturn { get; set; }
}

public class MonthlyReturnsData
{
    public string PortfolioId { get; set; } = null!;
    public List<MonthlyReturnItem> Returns { get; set; } = new();
    public List<int> Years { get; set; } = new();
}

public class MonthlyReturnItem
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ReturnPercent { get; set; }
}
