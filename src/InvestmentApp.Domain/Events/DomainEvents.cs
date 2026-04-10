using System;

namespace InvestmentApp.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public class TradeCreatedEvent : IDomainEvent
{
    public string TradeId { get; }
    public string PortfolioId { get; }
    public string Symbol { get; }
    public decimal Quantity { get; }
    public decimal Price { get; }
    public DateTime OccurredOn { get; }

    public TradeCreatedEvent(string tradeId, string portfolioId, string symbol, decimal quantity, decimal price)
    {
        TradeId = tradeId;
        PortfolioId = portfolioId;
        Symbol = symbol;
        Quantity = quantity;
        Price = price;
        OccurredOn = DateTime.UtcNow;
    }
}

public class TradeDeletedEvent : IDomainEvent
{
    public string TradeId { get; }
    public string PortfolioId { get; }
    public DateTime OccurredOn { get; }

    public TradeDeletedEvent(string tradeId, string portfolioId)
    {
        TradeId = tradeId;
        PortfolioId = portfolioId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class CapitalFlowRecordedEvent : IDomainEvent
{
    public string CapitalFlowId { get; }
    public string PortfolioId { get; }
    public string FlowType { get; }
    public decimal Amount { get; }
    public DateTime OccurredOn { get; }

    public CapitalFlowRecordedEvent(string capitalFlowId, string portfolioId, string flowType, decimal amount)
    {
        CapitalFlowId = capitalFlowId;
        PortfolioId = portfolioId;
        FlowType = flowType;
        Amount = amount;
        OccurredOn = DateTime.UtcNow;
    }
}

public class SnapshotTakenEvent : IDomainEvent
{
    public string SnapshotId { get; }
    public string PortfolioId { get; }
    public DateTime SnapshotDate { get; }
    public DateTime OccurredOn { get; }

    public SnapshotTakenEvent(string snapshotId, string portfolioId, DateTime snapshotDate)
    {
        SnapshotId = snapshotId;
        PortfolioId = portfolioId;
        SnapshotDate = snapshotDate;
        OccurredOn = DateTime.UtcNow;
    }
}

public class WorkerStartedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public class RiskProfileUpdatedEvent : IDomainEvent
{
    public string PortfolioId { get; }
    public DateTime OccurredOn { get; }

    public RiskProfileUpdatedEvent(string portfolioId)
    {
        PortfolioId = portfolioId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class StopLossTriggeredEvent : IDomainEvent
{
    public string TradeId { get; }
    public string Symbol { get; }
    public decimal StopLossPrice { get; }
    public decimal CurrentPrice { get; }
    public DateTime OccurredOn { get; }

    public StopLossTriggeredEvent(string tradeId, string symbol, decimal stopLossPrice, decimal currentPrice)
    {
        TradeId = tradeId;
        Symbol = symbol;
        StopLossPrice = stopLossPrice;
        CurrentPrice = currentPrice;
        OccurredOn = DateTime.UtcNow;
    }
}

public class TargetTriggeredEvent : IDomainEvent
{
    public string TradeId { get; }
    public string Symbol { get; }
    public decimal TargetPrice { get; }
    public decimal CurrentPrice { get; }
    public DateTime OccurredOn { get; }

    public TargetTriggeredEvent(string tradeId, string symbol, decimal targetPrice, decimal currentPrice)
    {
        TradeId = tradeId;
        Symbol = symbol;
        TargetPrice = targetPrice;
        CurrentPrice = currentPrice;
        OccurredOn = DateTime.UtcNow;
    }
}

public class StrategyCreatedEvent : IDomainEvent
{
    public string StrategyId { get; }
    public string Name { get; }
    public DateTime OccurredOn { get; }

    public StrategyCreatedEvent(string strategyId, string name)
    {
        StrategyId = strategyId;
        Name = name;
        OccurredOn = DateTime.UtcNow;
    }
}

public class TradeLinkedToStrategyEvent : IDomainEvent
{
    public string TradeId { get; }
    public string StrategyId { get; }
    public DateTime OccurredOn { get; }

    public TradeLinkedToStrategyEvent(string tradeId, string strategyId)
    {
        TradeId = tradeId;
        StrategyId = strategyId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class JournalCreatedEvent : IDomainEvent
{
    public string JournalId { get; }
    public string TradeId { get; }
    public DateTime OccurredOn { get; }

    public JournalCreatedEvent(string journalId, string tradeId)
    {
        JournalId = journalId;
        TradeId = tradeId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class AlertTriggeredEvent : IDomainEvent
{
    public string AlertRuleId { get; }
    public string AlertType { get; }
    public string UserId { get; }
    public DateTime OccurredOn { get; }

    public AlertTriggeredEvent(string alertRuleId, string alertType, string userId)
    {
        AlertRuleId = alertRuleId;
        AlertType = alertType;
        UserId = userId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class ScenarioNodeTriggeredEvent : IDomainEvent
{
    public string TradePlanId { get; }
    public string NodeId { get; }
    public string ActionType { get; }
    public string UserId { get; }
    public DateTime OccurredOn { get; }

    public ScenarioNodeTriggeredEvent(string tradePlanId, string nodeId, string actionType, string userId)
    {
        TradePlanId = tradePlanId;
        NodeId = nodeId;
        ActionType = actionType;
        UserId = userId;
        OccurredOn = DateTime.UtcNow;
    }
}

public class PlanReviewedEvent : IDomainEvent
{
    public string TradePlanId { get; }
    public string UserId { get; }
    public decimal PnLPercent { get; }
    public DateTime OccurredOn { get; }

    public PlanReviewedEvent(string tradePlanId, string userId, decimal pnlPercent)
    {
        TradePlanId = tradePlanId;
        UserId = userId;
        PnLPercent = pnlPercent;
        OccurredOn = DateTime.UtcNow;
    }
}