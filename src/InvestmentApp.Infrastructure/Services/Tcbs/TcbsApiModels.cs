using System.Text.Json.Serialization;

namespace InvestmentApp.Infrastructure.Services.Tcbs;

public class TcbsOverviewResponse
{
    [JsonPropertyName("ticker")] public string? Ticker { get; set; }
    [JsonPropertyName("exchange")] public string? Exchange { get; set; }
    [JsonPropertyName("industry")] public string? Industry { get; set; }
    [JsonPropertyName("companyType")] public string? CompanyType { get; set; }
    [JsonPropertyName("noShareholders")] public long? NoShareholders { get; set; }
    [JsonPropertyName("foreignPercent")] public decimal? ForeignPercent { get; set; }
    [JsonPropertyName("outstandingShare")] public long? OutstandingShare { get; set; }
    [JsonPropertyName("issueShare")] public long? IssueShare { get; set; }
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; set; }

    // Valuation
    [JsonPropertyName("pe")] public decimal? PE { get; set; }
    [JsonPropertyName("pb")] public decimal? PB { get; set; }
    [JsonPropertyName("dividendYield")] public decimal? DividendYield { get; set; }

    // Profitability
    [JsonPropertyName("roe")] public decimal? ROE { get; set; }
    [JsonPropertyName("roa")] public decimal? ROA { get; set; }
    [JsonPropertyName("eps")] public decimal? EPS { get; set; }

    // Financials
    [JsonPropertyName("revenue")] public decimal? Revenue { get; set; }
    [JsonPropertyName("revenueGrowth")] public decimal? RevenueGrowth { get; set; }
    [JsonPropertyName("netProfit")] public decimal? NetProfit { get; set; }
    [JsonPropertyName("netProfitGrowth")] public decimal? NetProfitGrowth { get; set; }

    // Leverage
    [JsonPropertyName("debtOnEquity")] public decimal? DebtOnEquity { get; set; }
    [JsonPropertyName("debtOnAsset")] public decimal? DebtOnAsset { get; set; }
    [JsonPropertyName("currentPayment")] public decimal? CurrentPayment { get; set; }
}
