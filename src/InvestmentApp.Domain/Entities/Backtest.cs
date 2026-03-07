using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public enum BacktestStatus { Pending, Running, Completed, Failed }

public class SimulatedTrade
{
    public string Symbol { get; set; } = string.Empty;
    public TradeType Type { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime ExitDate { get; set; }
    public decimal PnL { get; set; }
    public decimal ReturnPercent { get; set; }
}

public class BacktestResult
{
    public decimal FinalValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal CAGR { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public List<EquityCurvePoint> EquityCurve { get; set; } = new();
}

public class EquityCurvePoint
{
    public DateTime Date { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal DailyReturn { get; set; }
    public decimal CumulativeReturn { get; set; }
}

public class Backtest : AggregateRoot
{
    public string UserId { get; private set; }
    public string StrategyId { get; private set; }
    public string Name { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public decimal InitialCapital { get; private set; }
    public BacktestStatus Status { get; private set; }
    public BacktestResult? Result { get; private set; }
    public List<SimulatedTrade> SimulatedTrades { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public Backtest() { }

    public Backtest(string userId, string strategyId, string name,
        DateTime startDate, DateTime endDate, decimal initialCapital)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        StartDate = startDate;
        EndDate = endDate > startDate ? endDate : throw new ArgumentException("EndDate must be after StartDate");
        InitialCapital = initialCapital > 0 ? initialCapital : throw new ArgumentException("InitialCapital must be positive");
        Status = BacktestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRunning()
    {
        Status = BacktestStatus.Running;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Complete(BacktestResult result, List<SimulatedTrade> trades)
    {
        Result = result;
        SimulatedTrades = trades;
        Status = BacktestStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Fail(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = BacktestStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
