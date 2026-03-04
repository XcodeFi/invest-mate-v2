using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Market data provider interface for fetching real-time and historical prices.
/// </summary>
public interface IMarketDataProvider
{
    Task<StockPriceData?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<StockPriceData>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<Dictionary<string, StockPriceData>> GetBatchPricesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);
    Task<MarketIndexData?> GetIndexDataAsync(string indexSymbol, CancellationToken cancellationToken = default);
}

public class StockPriceData
{
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class MarketIndexData
{
    public string IndexSymbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
}
