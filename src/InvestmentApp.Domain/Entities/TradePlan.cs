using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class TradePlan : AggregateRoot
{
    public string UserId { get; private set; }
    public string? PortfolioId { get; private set; }

    // Core plan data
    public string Symbol { get; private set; }
    public string Direction { get; private set; }       // "Buy" | "Sell"
    public decimal EntryPrice { get; private set; }
    public decimal StopLoss { get; private set; }
    public decimal Target { get; private set; }
    public int Quantity { get; private set; }
    public string? StrategyId { get; private set; }
    public string MarketCondition { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }

    // Risk calculations (snapshot at plan creation)
    public decimal? RiskPercent { get; private set; }
    public decimal? AccountBalance { get; private set; }
    public decimal? RiskRewardRatio { get; private set; }
    public int ConfidenceLevel { get; private set; }    // 1-10

    // Checklist
    public List<ChecklistItem> Checklist { get; private set; }

    // Multi-lot entry support (nullable for backward compat)
    public EntryMode? EntryMode { get; private set; }
    public List<PlanLot>? Lots { get; private set; }

    // Exit planning
    public List<ExitTarget>? ExitTargets { get; private set; }
    public List<StopLossHistoryEntry>? StopLossHistory { get; private set; }

    // Lifecycle
    public TradePlanStatus Status { get; private set; }
    public string? TradeId { get; private set; }
    public List<string>? TradeIds { get; private set; }
    public DateTime? ExecutedAt { get; private set; }

    // Audit
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public TradePlan() { } // MongoDB

    public TradePlan(string userId, string symbol, string direction,
        decimal entryPrice, decimal stopLoss, decimal target, int quantity,
        string? portfolioId = null, string? strategyId = null,
        string? marketCondition = null, string? reason = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int confidenceLevel = 5,
        List<ChecklistItem>? checklist = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        PortfolioId = portfolioId;
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        Direction = direction ?? "Buy";
        EntryPrice = entryPrice;
        StopLoss = stopLoss;
        Target = target;
        Quantity = quantity;
        StrategyId = strategyId;
        MarketCondition = marketCondition ?? "Trending";
        Reason = reason;
        Notes = notes;
        RiskPercent = riskPercent;
        AccountBalance = accountBalance;
        RiskRewardRatio = riskRewardRatio;
        ConfidenceLevel = Math.Clamp(confidenceLevel, 1, 10);
        Checklist = checklist ?? new List<ChecklistItem>();
        Status = TradePlanStatus.Draft;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? symbol = null, string? direction = null,
        decimal? entryPrice = null, decimal? stopLoss = null, decimal? target = null,
        int? quantity = null, string? portfolioId = null, string? strategyId = null,
        string? marketCondition = null, string? reason = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int? confidenceLevel = null,
        List<ChecklistItem>? checklist = null)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot update an executed or reviewed plan");

        if (symbol != null) Symbol = symbol.ToUpper().Trim();
        if (direction != null) Direction = direction;
        if (entryPrice.HasValue) EntryPrice = entryPrice.Value;
        if (stopLoss.HasValue) StopLoss = stopLoss.Value;
        if (target.HasValue) Target = target.Value;
        if (quantity.HasValue) Quantity = quantity.Value;
        if (portfolioId != null) PortfolioId = portfolioId;
        if (strategyId != null) StrategyId = strategyId;
        if (marketCondition != null) MarketCondition = marketCondition;
        if (reason != null) Reason = reason;
        if (notes != null) Notes = notes;
        if (riskPercent.HasValue) RiskPercent = riskPercent;
        if (accountBalance.HasValue) AccountBalance = accountBalance;
        if (riskRewardRatio.HasValue) RiskRewardRatio = riskRewardRatio;
        if (confidenceLevel.HasValue) ConfidenceLevel = Math.Clamp(confidenceLevel.Value, 1, 10);
        if (checklist != null) Checklist = checklist;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReady()
    {
        if (Status != TradePlanStatus.Draft)
            throw new InvalidOperationException("Only draft plans can be marked ready");
        Status = TradePlanStatus.Ready;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkInProgress()
    {
        if (Status != TradePlanStatus.Ready && Status != TradePlanStatus.Draft)
            throw new InvalidOperationException("Only draft or ready plans can be marked in progress");
        Status = TradePlanStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Execute(string tradeId)
    {
        TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId));
        Status = TradePlanStatus.Executed;
        ExecutedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReviewed()
    {
        if (Status != TradePlanStatus.Executed)
            throw new InvalidOperationException("Only executed plans can be reviewed");
        Status = TradePlanStatus.Reviewed;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Cancel()
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot cancel an executed or reviewed plan");
        Status = TradePlanStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    // --- Multi-lot methods ---

    public void SetLots(EntryMode mode, List<PlanLot> lots)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot modify lots on executed/reviewed plan");
        EntryMode = mode;
        Lots = lots;
        Quantity = lots.Sum(l => l.PlannedQuantity);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void ExecuteLot(int lotNumber, string tradeId, decimal actualPrice)
    {
        if (Lots == null || Lots.Count == 0)
            throw new InvalidOperationException("Plan has no lots");
        var lot = Lots.FirstOrDefault(l => l.LotNumber == lotNumber)
            ?? throw new ArgumentException($"Lot {lotNumber} not found");
        if (lot.Status != PlanLotStatus.Pending)
            throw new InvalidOperationException($"Lot {lotNumber} is not pending");

        lot.Status = PlanLotStatus.Executed;
        lot.ActualPrice = actualPrice;
        lot.ExecutedAt = DateTime.UtcNow;
        lot.TradeId = tradeId;

        TradeIds ??= new List<string>();
        TradeIds.Add(tradeId);

        // Auto-transition status
        if (Status == TradePlanStatus.Ready || Status == TradePlanStatus.Draft)
            Status = TradePlanStatus.InProgress;

        if (Lots.All(l => l.Status != PlanLotStatus.Pending))
        {
            Status = TradePlanStatus.Executed;
            ExecutedAt = DateTime.UtcNow;
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    // --- Exit management methods ---

    public void SetExitTargets(List<ExitTarget> targets)
    {
        ExitTargets = targets;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void TriggerExitTarget(int level, string tradeId)
    {
        var target = ExitTargets?.FirstOrDefault(t => t.Level == level)
            ?? throw new ArgumentException($"Exit target level {level} not found");
        target.IsTriggered = true;
        target.TriggeredAt = DateTime.UtcNow;
        target.TradeId = tradeId;

        TradeIds ??= new List<string>();
        TradeIds.Add(tradeId);

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateStopLossWithHistory(decimal newStopLoss, string? reason = null)
    {
        StopLossHistory ??= new List<StopLossHistoryEntry>();
        StopLossHistory.Add(new StopLossHistoryEntry
        {
            OldPrice = StopLoss,
            NewPrice = newStopLoss,
            Reason = reason,
            ChangedAt = DateTime.UtcNow
        });
        StopLoss = newStopLoss;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}

public class ChecklistItem
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Checked { get; set; }
    public bool Critical { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public enum TradePlanStatus
{
    Draft,
    Ready,
    InProgress,
    Executed,
    Reviewed,
    Cancelled
}

public enum EntryMode
{
    Single,
    ScalingIn,
    DCA
}

public enum PlanLotStatus
{
    Pending,
    Executed,
    Cancelled
}

public class PlanLot
{
    public int LotNumber { get; set; }
    public decimal PlannedPrice { get; set; }
    public int PlannedQuantity { get; set; }
    public decimal? AllocationPercent { get; set; }
    public string? Label { get; set; }
    public PlanLotStatus Status { get; set; } = PlanLotStatus.Pending;
    public decimal? ActualPrice { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? TradeId { get; set; }
}

public enum ExitActionType
{
    TakeProfit,
    CutLoss,
    TrailingStop,
    PartialExit
}

public class ExitTarget
{
    public int Level { get; set; }
    public ExitActionType ActionType { get; set; }
    public decimal Price { get; set; }
    public int? Quantity { get; set; }
    public decimal? PercentOfPosition { get; set; }
    public string? Label { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}

public class StopLossHistoryEntry
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
