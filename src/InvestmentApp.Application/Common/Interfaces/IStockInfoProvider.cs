namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Extended stock information provider for market overview, stock details, search, etc.
/// </summary>
public interface IStockInfoProvider
{
    Task<StockDetailInfo?> GetStockDetailAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<MarketIndexInfo>> GetMarketOverviewAsync(CancellationToken cancellationToken = default);
    Task<List<StockSearchResult>> SearchStocksAsync(string keyword, CancellationToken cancellationToken = default);
    Task<List<TopStockInfo>> GetTopFluctuationAsync(string floor = "10", CancellationToken cancellationToken = default);
    Task<TradingHistorySummaryInfo?> GetTradingHistorySummaryAsync(string symbol, CancellationToken cancellationToken = default);
}

public class StockDetailInfo
{
    public string Symbol { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string CompanyNameEng { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string FloorCode { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal ReferencePrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CeilingPrice { get; set; }
    public decimal FloorPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal Value { get; set; }
    public decimal ForeignBuyVolume { get; set; }
    public decimal ForeignSellVolume { get; set; }
    public decimal ForeignRoom { get; set; }
    public string? LogoUrl { get; set; }
    public List<OrderBookLevel> Bids { get; set; } = new();
    public List<OrderBookLevel> Asks { get; set; } = new();
}

public class OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
}

public class MarketIndexInfo
{
    public string Symbol { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalValue { get; set; }
    public int? TradingStatus { get; set; }
    public decimal ForeignBuyValue { get; set; }
    public decimal ForeignSellValue { get; set; }
}

public class StockSearchResult
{
    public string Symbol { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? ShortName { get; set; }
    public string Exchange { get; set; } = null!;
    public string? LogoUrl { get; set; }
}

public class TopStockInfo
{
    public string Symbol { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ShortName { get; set; }
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal Volume { get; set; }
    public decimal CeilingPrice { get; set; }
    public decimal FloorPrice { get; set; }
    public decimal ReferencePrice { get; set; }
}

public class TradingHistorySummaryInfo
{
    public string Symbol { get; set; } = null!;
    public decimal ChangeDay { get; set; }
    public decimal ChangeWeek { get; set; }
    public decimal ChangeMonth { get; set; }
    public decimal Change3Month { get; set; }
    public decimal Change6Month { get; set; }
}
