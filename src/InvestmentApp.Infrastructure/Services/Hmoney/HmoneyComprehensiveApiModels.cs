using System.Text.Json.Serialization;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

// === Finance Indicators (/v2/ios/companies/index) ===

public class HmoneyFinanceIndicators
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("pe")]
    public decimal? PE { get; set; }

    [JsonPropertyName("pe4Q")]
    public decimal? PE4Q { get; set; }

    [JsonPropertyName("pb")]
    public decimal? PB { get; set; }

    [JsonPropertyName("pb4Q")]
    public decimal? PB4Q { get; set; }

    [JsonPropertyName("eps")]
    public decimal? EPS { get; set; }

    [JsonPropertyName("eps4Q")]
    public decimal? EPS4Q { get; set; }

    [JsonPropertyName("roe")]
    public decimal? ROE { get; set; }

    [JsonPropertyName("roe4Q")]
    public decimal? ROE4Q { get; set; }

    [JsonPropertyName("roa")]
    public decimal? ROA { get; set; }

    [JsonPropertyName("roa4Q")]
    public decimal? ROA4Q { get; set; }

    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("book_value")]
    public decimal? BookValue { get; set; }

    [JsonPropertyName("book_value4Q")]
    public decimal? BookValue4Q { get; set; }

    [JsonPropertyName("the_beta")]
    public decimal? Beta { get; set; }

    [JsonPropertyName("ev_per_ebitda")]
    public decimal? EvPerEbitda { get; set; }

    [JsonPropertyName("ev_per_ebit")]
    public decimal? EvPerEbit { get; set; }

    [JsonPropertyName("free_float_rate")]
    public decimal? FreeFloatRate { get; set; }

    [JsonPropertyName("min_52w")]
    public decimal? Min52W { get; set; }

    [JsonPropertyName("max_52w")]
    public decimal? Max52W { get; set; }

    [JsonPropertyName("listed_share_vol")]
    public long? ListedShareVol { get; set; }

    [JsonPropertyName("circulation_vol")]
    public long? CirculationVol { get; set; }

    [JsonPropertyName("group_name")]
    public string? GroupName { get; set; }

    [JsonPropertyName("audit_firm_name")]
    public string? AuditFirmName { get; set; }

    [JsonPropertyName("audit_is_big4")]
    public bool? AuditIsBig4 { get; set; }
}

// === Company Detail (/v1/ios/company/detail) ===

public class HmoneyCompanyDetail
{
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    [JsonPropertyName("floor")]
    public string? Floor { get; set; }

    [JsonPropertyName("major_share_holder")]
    public List<HmoneyShareholder>? MajorShareHolder { get; set; }

    [JsonPropertyName("company_leaders")]
    public List<HmoneyLeader>? CompanyLeaders { get; set; }
}

public class HmoneyShareholder
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("percentage")]
    public decimal Percentage { get; set; }
}

public class HmoneyLeader
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }
}

// === Financial Report (/v1/ios/company/financial-report) ===

public class HmoneyFinancialReportData
{
    [JsonPropertyName("header")]
    public List<string>? Header { get; set; }

    [JsonPropertyName("data")]
    public List<HmoneyFinancialReportRow>? Data { get; set; }

    [JsonPropertyName("total_page")]
    public int? TotalPage { get; set; }
}

public class HmoneyFinancialReportRow
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("values")]
    public List<decimal?>? Values { get; set; }

    [JsonPropertyName("expanded")]
    public bool? Expanded { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }
}

// === Company Plan (/v1/ios/company/plan) ===

public class HmoneyCompanyPlan
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("plan_revenue")]
    public decimal? PlanRevenue { get; set; }

    [JsonPropertyName("plan_profit")]
    public decimal? PlanProfit { get; set; }

    [JsonPropertyName("plan_dividend")]
    public decimal? PlanDividend { get; set; }
}

// === Dividend Events (/v1/ios/announcement/dividend-events) ===

public class HmoneyDividendEvent
{
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("ex_right_date")]
    public string? ExRightDate { get; set; }

    [JsonPropertyName("pay_date")]
    public string? PayDate { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("event_name")]
    public string? EventName { get; set; }
}

// === Peers (/v1/ios/stock-recommend/get_stock_related_bussiness) ===

public class HmoneyPeersData
{
    [JsonPropertyName("data")]
    public List<HmoneyPeerItem>? Data { get; set; }
}

public class HmoneyPeerItem
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("pe")]
    public decimal? PE { get; set; }

    [JsonPropertyName("pb")]
    public decimal? PB { get; set; }

    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal? ChangePercent { get; set; }
}

// === Foreign Trading Series (/v1/ios/stock/foreign-trading-series) ===

public class HmoneyForeignTradingItem
{
    [JsonPropertyName("trading_date")]
    public string? TradingDate { get; set; }

    [JsonPropertyName("buy_foreign_qtty")]
    public decimal BuyForeignQtty { get; set; }

    [JsonPropertyName("sell_foreign_qtty")]
    public decimal SellForeignQtty { get; set; }
}

// === Analyst Reports (/v1/ios/announcement/report-analytics) ===

public class HmoneyAnalystReportData
{
    [JsonPropertyName("data")]
    public List<HmoneyAnalystReport>? Data { get; set; }
}

public class HmoneyAnalystReport
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
