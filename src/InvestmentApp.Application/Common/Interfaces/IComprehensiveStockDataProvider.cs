namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Provider for comprehensive stock data used in AI analysis reports.
/// Aggregates financial indicators, company details, income statements,
/// peer comparison, news/events, and macro data from 24hmoney APIs.
/// </summary>
public interface IComprehensiveStockDataProvider
{
    Task<ComprehensiveStockData?> GetComprehensiveDataAsync(string symbol, CancellationToken ct = default);
}

public class ComprehensiveStockData
{
    public string Symbol { get; set; } = null!;

    // Section 1: Company Overview
    public CompanyOverview? Company { get; set; }

    // Section 2: Financial Indicators (P/E, P/B, ROE, ROA, EPS, etc.)
    public FinanceIndicators? Indicators { get; set; }

    // Section 3: Income Statement (quarterly revenue & profit)
    public List<IncomeStatementItem> IncomeStatements { get; set; } = new();

    // Section 4: Peer Comparison
    public List<PeerStock> Peers { get; set; } = new();

    // Section 5: News & Events
    public List<DividendEvent> DividendEvents { get; set; } = new();
    public CompanyPlan? BusinessPlan { get; set; }
    public List<AnalystReport> AnalystReports { get; set; } = new();

    // Section 6: Foreign Trading
    public List<ForeignTradingDay> ForeignTrading { get; set; } = new();

    // Section 7: Market Index (VN-Index)
    public MarketIndexSnapshot? MarketIndex { get; set; }
}

public class CompanyOverview
{
    public string? CompanyName { get; set; }
    public string? ShortName { get; set; }
    public string? Exchange { get; set; }
    public string? Industry { get; set; }
    public List<Shareholder> MajorShareholders { get; set; } = new();
    public List<CompanyLeader> Leaders { get; set; } = new();
    public long? ListedShares { get; set; }
    public long? OutstandingShares { get; set; }
    public decimal? FreeFloatRate { get; set; }        // %
}

public class Shareholder
{
    public string? Name { get; set; }
    public string? Position { get; set; }
    public decimal Quantity { get; set; }
    public decimal Percentage { get; set; }            // %
}

public class CompanyLeader
{
    public string? Name { get; set; }
    public string? Position { get; set; }
}

public class FinanceIndicators
{
    public decimal? PE { get; set; }
    public decimal? PE4Q { get; set; }
    public decimal? PB { get; set; }
    public decimal? PB4Q { get; set; }
    public decimal? EPS { get; set; }                  // VND
    public decimal? EPS4Q { get; set; }                // VND
    public decimal? ROE { get; set; }                  // %
    public decimal? ROE4Q { get; set; }                // %
    public decimal? ROA { get; set; }                  // %
    public decimal? ROA4Q { get; set; }                // %
    public decimal? MarketCap { get; set; }            // VND (raw)
    public decimal? BookValue { get; set; }            // VND per share
    public decimal? Beta { get; set; }
    public decimal? EvPerEbitda { get; set; }
    public decimal? EvPerEbit { get; set; }
    public decimal? FreeFloatRate { get; set; }        // %
    public decimal? Min52W { get; set; }
    public decimal? Max52W { get; set; }
    public string? IndustryGroup { get; set; }
    public string? AuditFirmName { get; set; }
    public bool? AuditIsBig4 { get; set; }
}

public class IncomeStatementItem
{
    public string? Period { get; set; }                // e.g. "Q1/2025"
    public decimal? Revenue { get; set; }              // tỷ VND
    public decimal? NetProfit { get; set; }            // tỷ VND
    public decimal? GrossProfit { get; set; }          // tỷ VND
}

public class PeerStock
{
    public string? Symbol { get; set; }
    public string? CompanyName { get; set; }
    public decimal? Price { get; set; }
    public decimal? PE { get; set; }
    public decimal? PB { get; set; }
    public decimal? MarketCap { get; set; }            // tỷ VND
    public decimal? ChangePercent { get; set; }        // %
}

public class DividendEvent
{
    public string? EventType { get; set; }             // "cash", "stock", etc.
    public string? Description { get; set; }
    public string? ExDate { get; set; }
    public string? PayDate { get; set; }
    public decimal? Value { get; set; }
}

public class CompanyPlan
{
    public int? Year { get; set; }
    public decimal? RevenuePlan { get; set; }          // tỷ VND
    public decimal? ProfitPlan { get; set; }           // tỷ VND
    public decimal? DividendPlan { get; set; }         // %
}

public class AnalystReport
{
    public string? Title { get; set; }
    public string? Source { get; set; }
    public string? PublishDate { get; set; }
    public string? Summary { get; set; }
}

public class ForeignTradingDay
{
    public string? Date { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
    public decimal NetVolume { get; set; }
}

public class MarketIndexSnapshot
{
    public decimal? Value { get; set; }
    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }
    public int? Advances { get; set; }
    public int? Declines { get; set; }
    public int? NoChange { get; set; }
    public decimal? TotalVolume { get; set; }          // triệu CP
    public decimal? TotalValue { get; set; }           // tỷ VND
    public decimal? ForeignBuyValue { get; set; }      // tỷ VND
    public decimal? ForeignSellValue { get; set; }     // tỷ VND
}
