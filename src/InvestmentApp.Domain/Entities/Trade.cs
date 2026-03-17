using System;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class Trade : AggregateRoot
{
    public string PortfolioId { get; private set; }
    public string Symbol { get; private set; }
    public TradeType TradeType { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public decimal Fee { get; private set; }
    public decimal Tax { get; private set; }
    public DateTime TradeDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? StrategyId { get; private set; }
    public string? TradePlanId { get; private set; }

    [BsonConstructor]
    public Trade() { } // For EF/MongoDB

    public Trade(string portfolioId, string symbol, TradeType tradeType, decimal quantity, decimal price, decimal fee = 0, decimal tax = 0, DateTime? tradeDate = null)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        TradeType = tradeType;
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be positive", nameof(quantity));
        Price = price > 0 ? price : throw new ArgumentException("Price must be positive", nameof(price));
        Fee = fee >= 0 ? fee : throw new ArgumentException("Fee must be non-negative", nameof(fee));
        Tax = tax >= 0 ? tax : throw new ArgumentException("Tax must be non-negative", nameof(tax));
        TradeDate = tradeDate ?? DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void LinkStrategy(string strategyId)
    {
        StrategyId = strategyId;
    }

    public void UnlinkStrategy()
    {
        StrategyId = null;
    }

    public void LinkTradePlan(string tradePlanId)
    {
        TradePlanId = tradePlanId;
    }

    public void UnlinkTradePlan()
    {
        TradePlanId = null;
    }
}

public enum TradeType
{
    BUY,
    SELL
}