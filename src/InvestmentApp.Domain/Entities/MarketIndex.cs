using System;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Represents a market index data point (e.g., VNINDEX, VN30).
/// </summary>
public class MarketIndex : AggregateRoot
{
    public string IndexSymbol { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public long Volume { get; private set; }
    public decimal Change { get; private set; }
    public decimal ChangePercent { get; private set; }

    [BsonConstructor]
    private MarketIndex() { } // For MongoDB

    public MarketIndex(string indexSymbol, DateTime date, decimal open, decimal high, decimal low, decimal close, long volume)
    {
        Id = Guid.NewGuid().ToString();
        IndexSymbol = indexSymbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(indexSymbol));
        Date = date.Date;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Change = close - open;
        ChangePercent = open > 0 ? ((close - open) / open) * 100 : 0;
    }

    public void UpdateData(decimal open, decimal high, decimal low, decimal close, long volume)
    {
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Change = close - open;
        ChangePercent = open > 0 ? ((close - open) / open) * 100 : 0;
    }
}
