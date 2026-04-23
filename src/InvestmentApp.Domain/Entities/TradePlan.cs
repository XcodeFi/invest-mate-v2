using InvestmentApp.Domain.Events;
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
    public string? Thesis { get; private set; }
    public string? Notes { get; private set; }

    // Thesis discipline (§D1 plan Vin-discipline)
    public List<InvalidationRule>? InvalidationCriteria { get; private set; }
    public DateTime? ExpectedReviewDate { get; private set; }
    public bool LegacyExempt { get; private set; } = false;

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

    // Scenario Playbook (Advanced exit strategy)
    public ExitStrategyMode ExitStrategyMode { get; private set; } = ExitStrategyMode.Simple;
    public List<ScenarioNode>? ScenarioNodes { get; private set; }

    // Time horizon (for campaign comparison)
    public TimeHorizon? TimeHorizon { get; private set; }

    // Campaign review data (set when plan is reviewed/closed)
    public CampaignReviewData? ReviewData { get; private set; }

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
        string? marketCondition = null, string? thesis = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int confidenceLevel = 5,
        List<ChecklistItem>? checklist = null,
        TimeHorizon? timeHorizon = null,
        List<InvalidationRule>? invalidationCriteria = null,
        DateTime? expectedReviewDate = null,
        bool legacyExempt = false)
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
        Thesis = thesis;
        Notes = notes;
        InvalidationCriteria = invalidationCriteria;
        ExpectedReviewDate = expectedReviewDate;
        LegacyExempt = legacyExempt;
        RiskPercent = riskPercent;
        AccountBalance = accountBalance;
        RiskRewardRatio = riskRewardRatio;
        ConfidenceLevel = Math.Clamp(confidenceLevel, 1, 10);
        Checklist = checklist ?? new List<ChecklistItem>();
        TimeHorizon = timeHorizon;
        Status = TradePlanStatus.Draft;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? symbol = null, string? direction = null,
        decimal? entryPrice = null, decimal? stopLoss = null, decimal? target = null,
        int? quantity = null, string? portfolioId = null, string? strategyId = null,
        string? marketCondition = null, string? thesis = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int? confidenceLevel = null,
        List<ChecklistItem>? checklist = null,
        TimeHorizon? timeHorizon = null,
        List<InvalidationRule>? invalidationCriteria = null,
        DateTime? expectedReviewDate = null)
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
        if (thesis != null) Thesis = thesis;
        if (notes != null) Notes = notes;
        if (riskPercent.HasValue) RiskPercent = riskPercent;
        if (accountBalance.HasValue) AccountBalance = accountBalance;
        if (riskRewardRatio.HasValue) RiskRewardRatio = riskRewardRatio;
        if (confidenceLevel.HasValue) ConfidenceLevel = Math.Clamp(confidenceLevel.Value, 1, 10);
        if (checklist != null) Checklist = checklist;
        if (timeHorizon.HasValue) TimeHorizon = timeHorizon.Value;
        if (invalidationCriteria != null) InvalidationCriteria = invalidationCriteria;
        if (expectedReviewDate.HasValue) ExpectedReviewDate = expectedReviewDate.Value;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReady()
    {
        if (Status == TradePlanStatus.Ready) return;
        if (Status != TradePlanStatus.Draft)
            throw new InvalidOperationException("Only draft plans can be marked ready");
        EnsureDisciplineGate();
        Status = TradePlanStatus.Ready;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkInProgress()
    {
        if (Status != TradePlanStatus.Ready)
            throw new InvalidOperationException("Only ready plans can be marked in progress");
        EnsureDisciplineGate();
        Status = TradePlanStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Gate kỷ luật size-based (§D3 plan Vin-discipline).
    /// Throw <see cref="InvalidOperationException"/> nếu plan vi phạm yêu cầu thesis / invalidation rule.
    /// </summary>
    private void EnsureDisciplineGate()
    {
        // Legacy exempt ở Draft → skip (tới T+3 hard gate release 2 sẽ bỏ nhánh này).
        if (LegacyExempt && Status == TradePlanStatus.Draft) return;

        decimal planSize = Quantity * EntryPrice;
        bool requireFullDiscipline =
            AccountBalance.HasValue
            && AccountBalance.Value > 0m
            && planSize >= AccountBalance.Value * 0.05m;

        if (requireFullDiscipline)
        {
            if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 30)
                throw new InvalidOperationException(
                    "Thesis ≥ 30 ký tự bắt buộc với plan size ≥ 5% tài khoản");
            if (InvalidationCriteria == null || InvalidationCriteria.Count == 0)
                throw new InvalidOperationException(
                    "Phải có ≥ 1 invalidation rule với plan size ≥ 5% tài khoản");
            if (InvalidationCriteria.Any(r => string.IsNullOrWhiteSpace(r.Detail) || r.Detail.Length < 20))
                throw new InvalidOperationException(
                    "Mỗi invalidation rule phải có detail ≥ 20 ký tự");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 15)
                throw new InvalidOperationException(
                    "Thesis ≥ 15 ký tự bắt buộc (dù plan size nhỏ)");
        }
    }

    public void SetThesis(string thesis)
    {
        if (string.IsNullOrWhiteSpace(thesis))
            throw new ArgumentException("Thesis không được rỗng", nameof(thesis));
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi thesis trên plan đã review");
        Thesis = thesis;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetInvalidationCriteria(List<InvalidationRule> rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi invalidation criteria trên plan đã review");
        InvalidationCriteria = rules;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetExpectedReviewDate(DateTime? date)
    {
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi expected review date trên plan đã review");
        ExpectedReviewDate = date;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Execute(string tradeId)
    {
        if (Status != TradePlanStatus.InProgress)
            throw new InvalidOperationException("Only in-progress plans can be executed");
        TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId));
        Status = TradePlanStatus.Executed;
        ExecutedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReviewed(CampaignReviewData reviewData)
    {
        if (Status != TradePlanStatus.Executed)
            throw new InvalidOperationException("Only executed plans can be reviewed");
        ReviewData = reviewData ?? throw new ArgumentNullException(nameof(reviewData));
        Status = TradePlanStatus.Reviewed;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new PlanReviewedEvent(Id, UserId, reviewData.PnLPercent));
    }

    public void UpdateReviewLessons(string lessonsLearned)
    {
        if (Status != TradePlanStatus.Reviewed || ReviewData == null)
            throw new InvalidOperationException("Can only update lessons on a reviewed plan");
        ReviewData.LessonsLearned = lessonsLearned;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetTimeHorizon(TimeHorizon horizon)
    {
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot change time horizon on a reviewed plan");
        TimeHorizon = horizon;
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

    public void Restore()
    {
        if (Status != TradePlanStatus.Cancelled)
            throw new InvalidOperationException("Only cancelled plans can be restored");
        Status = TradePlanStatus.Draft;
        TradeId = null;
        TradeIds?.Clear();
        ExecutedAt = null;

        // Clear IsTriggered flags on invalidation rules in case the cancellation came from AbortWithThesisInvalidation.
        // Rule text is preserved so user doesn't have to rewrite.
        if (InvalidationCriteria != null)
        {
            foreach (var rule in InvalidationCriteria)
            {
                rule.IsTriggered = false;
                rule.TriggeredAt = null;
            }
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Abort plan vì thesis đã sai (§D4 plan Vin-discipline).
    /// Áp cho state Ready | InProgress | Executed (B3 fix — multi-lot partial-executed).
    /// Throw khi Draft (dùng Cancel) hoặc Reviewed/Cancelled (terminal).
    /// </summary>
    public void AbortWithThesisInvalidation(InvalidationTrigger trigger, string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail không được rỗng khi abort thesis", nameof(detail));

        if (Status != TradePlanStatus.Ready
            && Status != TradePlanStatus.InProgress
            && Status != TradePlanStatus.Executed)
        {
            throw new InvalidOperationException(
                $"Không thể abort plan ở trạng thái {Status}. Chỉ áp dụng cho Ready/InProgress/Executed.");
        }

        InvalidationCriteria ??= new List<InvalidationRule>();
        InvalidationCriteria.Add(new InvalidationRule
        {
            Trigger = trigger,
            Detail = detail,
            IsTriggered = true,
            TriggeredAt = DateTime.UtcNow
        });

        // Ready: không có trade → set Cancelled luôn.
        // InProgress: set Cancelled (service layer chịu trách nhiệm exit trades nếu cần).
        // Executed: giữ nguyên status — position đã mở, exit flow do application/service layer lo.
        if (Status == TradePlanStatus.Ready || Status == TradePlanStatus.InProgress)
        {
            Status = TradePlanStatus.Cancelled;
        }

        AddDomainEvent(new TradePlanThesisInvalidatedEvent(
            Id, UserId, trigger, detail,
            TradeIds?.ToList() ?? (TradeId != null ? new List<string> { TradeId } : new List<string>())));

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
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed || Status == TradePlanStatus.Cancelled)
            throw new InvalidOperationException($"Cannot execute lot on a {Status} plan");

        lot.Status = PlanLotStatus.Executed;
        lot.ActualPrice = actualPrice;
        lot.ExecutedAt = DateTime.UtcNow;
        lot.TradeId = tradeId;

        TradeIds ??= new List<string>();
        TradeIds.Add(tradeId);

        // Auto-transition status: Draft/Ready → InProgress; all-lots-executed → Executed
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

    // --- Scenario Playbook methods ---

    public void SetExitStrategyMode(ExitStrategyMode mode)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot change exit strategy mode on executed/reviewed plan");
        ExitStrategyMode = mode;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetScenarioNodes(List<ScenarioNode> nodes)
    {
        if (ExitStrategyMode != ExitStrategyMode.Advanced)
            throw new InvalidOperationException("Cannot set scenario nodes in Simple mode");
        if (!nodes.Any(n => n.ParentId == null))
            throw new ArgumentException("Scenario tree must have at least one root node");
        var nodeIds = nodes.Select(n => n.NodeId).ToHashSet();
        if (nodes.Any(n => n.ParentId != null && !nodeIds.Contains(n.ParentId)))
            throw new ArgumentException("ScenarioNode references a non-existent parent");
        ScenarioNodes = nodes;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void TriggerScenarioNode(string nodeId, string? tradeId = null)
    {
        var node = ScenarioNodes?.FirstOrDefault(n => n.NodeId == nodeId)
            ?? throw new ArgumentException($"Scenario node {nodeId} not found");
        if (node.Status != ScenarioNodeStatus.Pending)
            throw new InvalidOperationException($"Scenario node {nodeId} is not pending");
        if (node.ParentId != null)
        {
            var parent = ScenarioNodes!.First(n => n.NodeId == node.ParentId);
            if (parent.Status != ScenarioNodeStatus.Triggered)
                throw new InvalidOperationException("Parent scenario must be triggered first");
        }
        node.Status = ScenarioNodeStatus.Triggered;
        node.TriggeredAt = DateTime.UtcNow;
        node.TradeId = tradeId;
        if (tradeId != null)
        {
            TradeIds ??= new List<string>();
            TradeIds.Add(tradeId);
        }
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new ScenarioNodeTriggeredEvent(Id, nodeId, node.ActionType.ToString(), UserId));
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

// --- Time Horizon ---

public enum TimeHorizon
{
    ShortTerm,    // < 3 months
    MediumTerm,   // 3-12 months
    LongTerm      // > 1 year
}

// --- Campaign Review ---

public class CampaignReviewData
{
    public decimal PnLAmount { get; set; }
    public decimal PnLPercent { get; set; }
    public int HoldingDays { get; set; }
    public decimal PnLPerDay { get; set; }
    public decimal AnnualizedReturnPercent { get; set; }
    public decimal TargetAchievementPercent { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalReturned { get; set; }
    public decimal TotalFees { get; set; }
    public string? LessonsLearned { get; set; }
    public DateTime ReviewedAt { get; set; }
}

// --- Scenario Playbook types ---

public enum ExitStrategyMode
{
    Simple,
    Advanced
}

public enum ScenarioConditionType
{
    PriceAbove,
    PriceBelow,
    PricePercentChange,
    TrailingStopHit,
    TimeElapsed
}

public enum ScenarioActionType
{
    SellPercent,
    SellAll,
    MoveStopLoss,
    MoveStopToBreakeven,
    ActivateTrailingStop,
    AddPosition,
    SendNotification
}

public enum ScenarioNodeStatus
{
    Pending,
    Triggered,
    Skipped,
    Expired
}

public enum TrailingStopMethod
{
    Percentage,
    ATR,
    FixedAmount
}

public class TrailingStopConfig
{
    public TrailingStopMethod Method { get; set; } = TrailingStopMethod.Percentage;
    public decimal TrailValue { get; set; }
    public decimal? ActivationPrice { get; set; }
    public decimal? StepSize { get; set; }
    public decimal? CurrentTrailingStop { get; set; }
    public decimal? HighestPrice { get; set; }
}

public class ScenarioNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;

    // Condition
    public ScenarioConditionType ConditionType { get; set; }
    public decimal? ConditionValue { get; set; }
    public string? ConditionNote { get; set; }

    // Action
    public ScenarioActionType ActionType { get; set; }
    public decimal? ActionValue { get; set; }
    public TrailingStopConfig? TrailingStopConfig { get; set; }

    // Status
    public ScenarioNodeStatus Status { get; set; } = ScenarioNodeStatus.Pending;
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}
