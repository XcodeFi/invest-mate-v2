using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Represents a point-in-time snapshot of a portfolio's state.
/// Used for time-travel queries and historical performance tracking.
/// </summary>
public class PortfolioSnapshotEntity : AggregateRoot
{
    public string PortfolioId { get; private set; }
    public DateTime SnapshotDate { get; private set; }
    public decimal TotalValue { get; private set; }
    public decimal CashBalance { get; private set; }
    public decimal InvestedValue { get; private set; }
    public decimal UnrealizedPnL { get; private set; }
    public decimal RealizedPnL { get; private set; }
    public decimal DailyReturn { get; private set; }
    public decimal CumulativeReturn { get; private set; }
    public List<PositionSnapshotItem> Positions { get; private set; }
    public DateTime CreatedAt { get; private set; }

    [BsonConstructor]
    private PortfolioSnapshotEntity() { } // For MongoDB

    public PortfolioSnapshotEntity(
        string portfolioId,
        DateTime snapshotDate,
        decimal totalValue,
        decimal cashBalance,
        decimal investedValue,
        decimal unrealizedPnL,
        decimal realizedPnL,
        decimal dailyReturn,
        decimal cumulativeReturn,
        List<PositionSnapshotItem>? positions = null)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        SnapshotDate = snapshotDate.Date;
        TotalValue = totalValue;
        CashBalance = cashBalance;
        InvestedValue = investedValue;
        UnrealizedPnL = unrealizedPnL;
        RealizedPnL = realizedPnL;
        DailyReturn = dailyReturn;
        CumulativeReturn = cumulativeReturn;
        Positions = positions ?? new List<PositionSnapshotItem>();
        CreatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// A snapshot of a single position within a portfolio at a given date.
/// </summary>
public class PositionSnapshotItem
{
    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal MarketPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal Weight { get; set; }
}
