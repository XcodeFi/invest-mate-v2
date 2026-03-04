using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Service for calculating strategy performance metrics.
/// </summary>
public interface IStrategyPerformanceService
{
    Task<StrategyPerformanceSummary> GetPerformanceAsync(string strategyId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StrategyComparisonItem>> CompareStrategiesAsync(IEnumerable<string> strategyIds, string userId, CancellationToken cancellationToken = default);
}

public class StrategyPerformanceSummary
{
    public string StrategyId { get; set; } = null!;
    public string StrategyName { get; set; } = null!;
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal AveragePnL { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
}

public class StrategyComparisonItem
{
    public string StrategyId { get; set; } = null!;
    public string StrategyName { get; set; } = null!;
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal ProfitFactor { get; set; }
}
