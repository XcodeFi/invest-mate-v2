using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Stop-loss and target price tracking for individual trades.
/// </summary>
public class StopLossTarget : AggregateRoot
{
    public string TradeId { get; private set; }
    public string PortfolioId { get; private set; }
    public string UserId { get; private set; }
    public string Symbol { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal StopLossPrice { get; private set; }
    public decimal TargetPrice { get; private set; }
    public decimal? TrailingStopPercent { get; private set; }
    public decimal? TrailingStopPrice { get; private set; }
    public bool IsStopLossTriggered { get; private set; }
    public bool IsTargetTriggered { get; private set; }
    public DateTime? TriggeredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public StopLossTarget() { } // MongoDB

    public StopLossTarget(string tradeId, string portfolioId, string userId, string symbol,
        decimal entryPrice, decimal stopLossPrice, decimal targetPrice,
        decimal? trailingStopPercent = null)
    {
        Id = Guid.NewGuid().ToString();
        TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId));
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        EntryPrice = entryPrice > 0 ? entryPrice : throw new ArgumentException("Entry price must be positive");
        StopLossPrice = stopLossPrice;
        TargetPrice = targetPrice;
        TrailingStopPercent = trailingStopPercent;
        IsStopLossTriggered = false;
        IsTargetTriggered = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStopLoss(decimal stopLossPrice)
    {
        StopLossPrice = stopLossPrice;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateTarget(decimal targetPrice)
    {
        TargetPrice = targetPrice;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateTrailingStop(decimal trailingStopPercent, decimal currentPrice)
    {
        TrailingStopPercent = trailingStopPercent;
        TrailingStopPrice = currentPrice * (1 - trailingStopPercent / 100m);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void TriggerStopLoss()
    {
        IsStopLossTriggered = true;
        TriggeredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TriggerTarget()
    {
        IsTargetTriggered = true;
        TriggeredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculate risk/reward ratio
    /// </summary>
    public decimal GetRiskRewardRatio()
    {
        var risk = EntryPrice - StopLossPrice;
        if (risk <= 0) return 0;
        var reward = TargetPrice - EntryPrice;
        return reward / risk;
    }

    /// <summary>
    /// Calculate risk amount per share
    /// </summary>
    public decimal GetRiskPerShare()
    {
        return EntryPrice - StopLossPrice;
    }
}
