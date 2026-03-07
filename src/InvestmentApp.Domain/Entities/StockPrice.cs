using System;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Represents a historical stock price record.
/// </summary>
public class StockPrice : AggregateRoot
{
    public string Symbol { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public long Volume { get; private set; }
    public string Source { get; private set; }
    public DateTime FetchedAt { get; private set; }

    [BsonConstructor]
    public StockPrice() { } // For MongoDB

    public StockPrice(string symbol, DateTime date, decimal open, decimal high, decimal low, decimal close, long volume, string source = "Manual")
    {
        Id = Guid.NewGuid().ToString();
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        Date = date.Date;
        Open = open >= 0 ? open : throw new ArgumentException("Open price must be non-negative", nameof(open));
        High = high >= 0 ? high : throw new ArgumentException("High price must be non-negative", nameof(high));
        Low = low >= 0 ? low : throw new ArgumentException("Low price must be non-negative", nameof(low));
        Close = close >= 0 ? close : throw new ArgumentException("Close price must be non-negative", nameof(close));
        Volume = volume >= 0 ? volume : throw new ArgumentException("Volume must be non-negative", nameof(volume));
        Source = source;
        FetchedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal open, decimal high, decimal low, decimal close, long volume)
    {
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        FetchedAt = DateTime.UtcNow;
    }
}
