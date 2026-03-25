using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;

namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Service for calculating position-level and portfolio-level risk metrics.
/// </summary>
public interface IRiskCalculationService
{
    /// <summary>
    /// Calculates position size as percentage of total portfolio value.
    /// </summary>
    Task<decimal> CalculatePositionSizePercentAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets risk metrics for all positions in a portfolio.
    /// </summary>
    Task<PortfolioRiskSummary> GetPortfolioRiskSummaryAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates maximum drawdown from historical snapshots.
    /// </summary>
    Task<DrawdownResult> CalculateMaxDrawdownAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Value at Risk (95% confidence) using parametric method.
    /// </summary>
    Task<decimal> CalculateValueAtRiskAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates symbol correlation matrix for portfolio positions.
    /// </summary>
    Task<CorrelationMatrix> CalculateCorrelationMatrixAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes portfolio for optimization: concentration, sector diversification, correlation warnings.
    /// </summary>
    Task<PortfolioOptimizationResult> GetPortfolioOptimizationAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets trailing stop alerts with real-time price comparison.
    /// </summary>
    Task<TrailingStopAlertsResult> GetTrailingStopAlertsAsync(string portfolioId, CancellationToken cancellationToken = default);
}

public class PortfolioRiskSummary
{
    public string PortfolioId { get; set; } = null!;
    public decimal TotalValue { get; set; }
    public List<PositionRiskItem> Positions { get; set; } = new();
    public decimal MaxDrawdown { get; set; }
    public decimal ValueAtRisk95 { get; set; }
    public decimal LargestPositionPercent { get; set; }
    public int PositionCount { get; set; }
}

public class PositionRiskItem
{
    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal PositionSizePercent { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public decimal? RiskPerShare { get; set; }
    public decimal? RiskAmount { get; set; }
    public decimal DistanceToStopLossPercent { get; set; }
    public decimal DistanceToTargetPercent { get; set; }
    public string? Sector { get; set; }
    public decimal? Beta { get; set; }
    public decimal? PositionVaR { get; set; }
}

public class DrawdownResult
{
    public string PortfolioId { get; set; } = null!;
    public decimal MaxDrawdownPercent { get; set; }
    public decimal CurrentDrawdownPercent { get; set; }
    public DateTime? PeakDate { get; set; }
    public decimal? PeakValue { get; set; }
    public DateTime? TroughDate { get; set; }
    public decimal? TroughValue { get; set; }
    public List<DrawdownPoint> DrawdownSeries { get; set; } = new();
}

public class DrawdownPoint
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public decimal DrawdownPercent { get; set; }
}

public class CorrelationMatrix
{
    public string PortfolioId { get; set; } = null!;
    public List<string> Symbols { get; set; } = new();
    public decimal[,]? Matrix { get; set; }
    public List<CorrelationPair> Pairs { get; set; } = new();
}

public class CorrelationPair
{
    public string Symbol1 { get; set; } = null!;
    public string Symbol2 { get; set; } = null!;
    public decimal Correlation { get; set; }
}
