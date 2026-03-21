namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Provider for stock fundamental financial data (P/E, EPS, ROE, etc.).
/// </summary>
public interface IFundamentalDataProvider
{
    Task<StockFundamentalData?> GetFundamentalsAsync(string symbol, CancellationToken ct = default);
}

public class StockFundamentalData
{
    public string Symbol { get; set; } = null!;
    public string? Industry { get; set; }
    public decimal? MarketCap { get; set; }          // tỷ VND
    public decimal? PE { get; set; }
    public decimal? PB { get; set; }
    public decimal? EPS { get; set; }                // VND
    public decimal? ROE { get; set; }                // %
    public decimal? ROA { get; set; }                // %
    public decimal? DebtToEquity { get; set; }
    public decimal? DividendYield { get; set; }      // %
    public decimal? Revenue { get; set; }            // tỷ VND
    public decimal? RevenueGrowth { get; set; }      // %
    public decimal? NetProfit { get; set; }          // tỷ VND
    public decimal? NetProfitGrowth { get; set; }    // %
    public decimal? ForeignPercent { get; set; }     // %
    public long? OutstandingShares { get; set; }
}
