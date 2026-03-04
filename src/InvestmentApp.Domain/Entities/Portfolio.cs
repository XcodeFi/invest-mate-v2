using System;
using InvestmentApp.Domain.Events;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class Portfolio : AggregateRoot
{
    public string UserId { get; private set; }
    public string Name { get; private set; }
    public decimal InitialCapital { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private readonly List<Trade> _trades = new();
    public IReadOnlyCollection<Trade> Trades => _trades.AsReadOnly();

    [BsonConstructor]
    private Portfolio() { } // For EF/MongoDB

    public Portfolio(string userId, string name, decimal initialCapital)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        InitialCapital = initialCapital >= 0 ? initialCapital : throw new ArgumentException("Initial capital must be non-negative", nameof(initialCapital));
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
    }

    public void AddTrade(Trade trade)
    {
        if (trade == null) throw new ArgumentNullException(nameof(trade));
        if (trade.PortfolioId != Id) throw new InvalidOperationException("Trade does not belong to this portfolio");

        _trades.Add(trade);
        AddDomainEvent(new TradeCreatedEvent(trade.Id, Id, trade.Symbol, trade.Quantity, trade.Price));
        IncrementVersion();
    }

    public void RemoveTrade(string tradeId)
    {
        var trade = _trades.FirstOrDefault(t => t.Id == tradeId);
        if (trade == null) throw new InvalidOperationException("Trade not found");

        _trades.Remove(trade);
        AddDomainEvent(new TradeDeletedEvent(tradeId, Id));
        IncrementVersion();
    }
}