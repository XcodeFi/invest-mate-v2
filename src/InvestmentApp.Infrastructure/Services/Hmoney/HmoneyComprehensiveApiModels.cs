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
    [JsonPropertyName("ownership")]
    public List<HmoneyShareholder>? Ownership { get; set; }

    [JsonPropertyName("leadership")]
    public List<HmoneyLeader>? Leadership { get; set; }

    [JsonPropertyName("intro")]
    public string? Intro { get; set; }
}

// Tỷ lệ và số cổ phiếu về dưới dạng CHUỖI ("16.07", "29863050").
public class HmoneyShareholder
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("stock")]
    public string? Stock { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }
}

public class HmoneyLeader
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("positions")]
    public List<HmoneyLeaderPosition>? Positions { get; set; }
}

public class HmoneyLeaderPosition
{
    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }
}

// === Financial Report (/v1/ios/company/financial-report) ===

public class HmoneyFinancialReportData
{
    [JsonPropertyName("headers")]
    public List<HmoneyReportPeriod>? Headers { get; set; }

    [JsonPropertyName("rows")]
    public List<HmoneyFinancialReportRow>? Rows { get; set; }
}

public class HmoneyReportPeriod
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("quarter")]
    public int? Quarter { get; set; }
}

public class HmoneyFinancialReportRow
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("values")]
    public List<decimal?>? Values { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }
}

// === Company Plan (/v1/ios/company/plan) ===

public class HmoneyCompanyPlanData
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("quarter")]
    public int? Quarter { get; set; }

    [JsonPropertyName("plan")]
    public List<HmoneyCompanyPlanItem>? Plan { get; set; }
}

public class HmoneyCompanyPlanItem
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("expect")]
    public decimal? Expect { get; set; }

    [JsonPropertyName("current")]
    public decimal? Current { get; set; }

    [JsonPropertyName("percent")]
    public decimal? Percent { get; set; }
}

// === Dividend Events (/v1/ios/announcement/dividend-events) ===

// Các mốc ngày về dưới dạng epoch giây, neo vào nửa đêm giờ Việt Nam.
public class HmoneyDividendEvent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("exright_date")]
    public long? ExRightDate { get; set; }

    [JsonPropertyName("payout_date")]
    public long? PayoutDate { get; set; }

    [JsonPropertyName("record_date")]
    public long? RecordDate { get; set; }
}

// === Peers (/v1/ios/stock-recommend/get_stock_related_bussiness) ===

// Danh sách cùng ngành được chia theo sàn; "all" là rổ gộp.
public class HmoneyPeersData
{
    [JsonPropertyName("all")]
    public HmoneyPeersBucket? All { get; set; }
}

public class HmoneyPeersBucket
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

// `data_time` là diễn biến trong phiên (mỗi 5 phút) nên không dùng cho hồ sơ công ty;
// chỉ lấy các mức tổng hợp, đơn vị tỷ VND.
public class HmoneyForeignTradingData
{
    [JsonPropertyName("today_buy_value")]
    public decimal? TodayBuyValue { get; set; }

    [JsonPropertyName("today_sell_value")]
    public decimal? TodaySellValue { get; set; }

    [JsonPropertyName("week_buy_value")]
    public decimal? WeekBuyValue { get; set; }

    [JsonPropertyName("week_sell_value")]
    public decimal? WeekSellValue { get; set; }

    [JsonPropertyName("month_buy_value")]
    public decimal? MonthBuyValue { get; set; }

    [JsonPropertyName("month_sell_value")]
    public decimal? MonthSellValue { get; set; }
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
