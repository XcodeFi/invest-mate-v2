using System.Text.Json.Serialization;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

/// <summary>
/// Response models for 24hmoney.vn API endpoints.
/// All property names use JsonPropertyName to match the snake_case API responses.
/// </summary>

// Base response envelope
public class HmoneyResponse<T>
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

// === Stock Detail ===

public class HmoneyStockDetailData
{
    [JsonPropertyName("share_detail")]
    public HmoneyShareDetail? ShareDetail { get; set; }
}

public class HmoneyShareDetail
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_name_eng")]
    public string? CompanyNameEng { get; set; }

    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    [JsonPropertyName("stock_exchange")]
    public string? StockExchange { get; set; }

    [JsonPropertyName("floor_code")]
    public string? FloorCode { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("match_price")]
    public decimal MatchPrice { get; set; }

    [JsonPropertyName("change")]
    public decimal Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal ChangePercent { get; set; }

    [JsonPropertyName("basic_price")]
    public decimal BasicPrice { get; set; }

    [JsonPropertyName("open_price")]
    public decimal OpenPrice { get; set; }

    [JsonPropertyName("close_price")]
    public decimal ClosePrice { get; set; }

    [JsonPropertyName("hieghest_price")]
    public decimal HighestPrice { get; set; } // typo in 24hmoney API

    [JsonPropertyName("lowest_price")]
    public decimal LowestPrice { get; set; }

    [JsonPropertyName("average_price")]
    public decimal AveragePrice { get; set; }

    [JsonPropertyName("ceiling_price")]
    public decimal CeilingPrice { get; set; }

    [JsonPropertyName("floor_price")]
    public decimal FloorPrice { get; set; }

    [JsonPropertyName("accumulated_val")]
    public decimal AccumulatedVal { get; set; }

    [JsonPropertyName("accumylated_vol")]
    public decimal AccumulatedVol { get; set; } // typo in 24hmoney API

    [JsonPropertyName("buy_foreign_qtty")]
    public decimal BuyForeignQtty { get; set; }

    [JsonPropertyName("sell_foreign_qtty")]
    public decimal SellForeignQtty { get; set; }

    [JsonPropertyName("foreign_current_room")]
    public decimal ForeignCurrentRoom { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    // 3-level bid book
    [JsonPropertyName("bid_price01")]
    public decimal BidPrice01 { get; set; }
    [JsonPropertyName("bid_qtty01")]
    public decimal BidQtty01 { get; set; }
    [JsonPropertyName("bid_price02")]
    public decimal BidPrice02 { get; set; }
    [JsonPropertyName("bid_qtty02")]
    public decimal BidQtty02 { get; set; }
    [JsonPropertyName("bid_price03")]
    public decimal BidPrice03 { get; set; }
    [JsonPropertyName("bid_qtty03")]
    public decimal BidQtty03 { get; set; }

    // 3-level ask book
    [JsonPropertyName("offer_price01")]
    public decimal OfferPrice01 { get; set; }
    [JsonPropertyName("offer_qtty01")]
    public decimal OfferQtty01 { get; set; }
    [JsonPropertyName("offer_price02")]
    public decimal OfferPrice02 { get; set; }
    [JsonPropertyName("offer_qtty02")]
    public decimal OfferQtty02 { get; set; }
    [JsonPropertyName("offer_price03")]
    public decimal OfferPrice03 { get; set; }
    [JsonPropertyName("offer_qtty03")]
    public decimal OfferQtty03 { get; set; }
}

// === Graph (Chart) Data ===

public class HmoneyGraphData
{
    [JsonPropertyName("basic_price")]
    public decimal BasicPrice { get; set; }

    [JsonPropertyName("points")]
    public List<HmoneyGraphPoint> Points { get; set; } = new();
}

public class HmoneyGraphPoint
{
    [JsonPropertyName("x")]
    public long X { get; set; } // unix timestamp

    [JsonPropertyName("y")]
    public decimal Y { get; set; } // price

    [JsonPropertyName("z")]
    public long Z { get; set; } // volume
}

// === Market Indices ===

public class HmoneyIndicesData
{
    [JsonPropertyName("floor")]
    public List<HmoneyIndexItem> Floor { get; set; } = new();

    [JsonPropertyName("derivative")]
    public List<HmoneyIndexItem>? Derivative { get; set; }
}

public class HmoneyIndexItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("change")]
    public decimal Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal ChangePercent { get; set; }

    [JsonPropertyName("total_share_traded")]
    public decimal TotalShareTraded { get; set; }

    [JsonPropertyName("total_value_traded")]
    public decimal TotalValueTraded { get; set; }

    [JsonPropertyName("floor_code")]
    public string? FloorCode { get; set; }

    [JsonPropertyName("trading_status")]
    public int? TradingStatus { get; set; }

    [JsonPropertyName("foreign_today_buy_value")]
    public decimal ForeignTodayBuyValue { get; set; }

    [JsonPropertyName("foreign_today_sell_value")]
    public decimal ForeignTodaySellValue { get; set; }
}

// === Index Detail (/v1/ios/indices/detail?floor_code=) ===

public class HmoneyIndexDetailData
{
    [JsonPropertyName("share_detail")]
    public HmoneyIndexShareDetail? ShareDetail { get; set; }

    [JsonPropertyName("statistic")]
    public HmoneyIndexStatistic? Statistic { get; set; }
}

public class HmoneyIndexShareDetail
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("floor_code")]
    public string? FloorCode { get; set; }

    [JsonPropertyName("market_index")]
    public decimal MarketIndex { get; set; }

    [JsonPropertyName("prior_market_index")]
    public decimal PriorMarketIndex { get; set; }

    [JsonPropertyName("change")]
    public decimal Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal ChangePercent { get; set; }

    [JsonPropertyName("avg_index")]
    public decimal AvgIndex { get; set; }

    [JsonPropertyName("highest_index")]
    public decimal HighestIndex { get; set; }

    [JsonPropertyName("lowest_index")]
    public decimal LowestIndex { get; set; }

    [JsonPropertyName("advance")]
    public int Advance { get; set; }

    [JsonPropertyName("decline")]
    public int Decline { get; set; }

    [JsonPropertyName("no_change")]
    public int NoChange { get; set; }

    [JsonPropertyName("ceiling")]
    public int Ceiling { get; set; }

    [JsonPropertyName("floor")]
    public int Floor { get; set; }

    [JsonPropertyName("accumulated_vol")]
    public decimal AccumulatedVol { get; set; }

    [JsonPropertyName("accumulated_val")]
    public decimal AccumulatedVal { get; set; }

    [JsonPropertyName("foreign_today_buy_value")]
    public decimal ForeignTodayBuyValue { get; set; }

    [JsonPropertyName("foreign_today_sell_value")]
    public decimal ForeignTodaySellValue { get; set; }

    [JsonPropertyName("foreign_week_buy_value")]
    public decimal ForeignWeekBuyValue { get; set; }

    [JsonPropertyName("foreign_week_sell_value")]
    public decimal ForeignWeekSellValue { get; set; }

    [JsonPropertyName("foreign_month_buy_value")]
    public decimal ForeignMonthBuyValue { get; set; }

    [JsonPropertyName("foreign_month_sell_value")]
    public decimal ForeignMonthSellValue { get; set; }

    [JsonPropertyName("updated_at")]
    public decimal UpdatedAt { get; set; }
}

public class HmoneyIndexStatistic
{
    [JsonPropertyName("floor_code")]
    public string? FloorCode { get; set; }

    [JsonPropertyName("stock_increase")]
    public int StockIncrease { get; set; }

    [JsonPropertyName("stock_decrease")]
    public int StockDecrease { get; set; }

    [JsonPropertyName("stock_unchanged")]
    public int StockUnchanged { get; set; }

    [JsonPropertyName("stock_ceiling")]
    public int StockCeiling { get; set; }

    [JsonPropertyName("stock_floor")]
    public int StockFloor { get; set; }

    [JsonPropertyName("buy_foreign_qtty")]
    public decimal BuyForeignQtty { get; set; }

    [JsonPropertyName("sell_foreign_qtty")]
    public decimal SellForeignQtty { get; set; }

    [JsonPropertyName("total_val_increase")]
    public decimal TotalValIncrease { get; set; }

    [JsonPropertyName("total_val_decrease")]
    public decimal TotalValDecrease { get; set; }

    [JsonPropertyName("total_val_unchanged")]
    public decimal TotalValUnchanged { get; set; }

    [JsonPropertyName("total_val_ceiling")]
    public decimal TotalValCeiling { get; set; }

    [JsonPropertyName("total_val_floor")]
    public decimal TotalValFloor { get; set; }
}

// === Company List (for search) ===

public class HmoneyCompanyItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_name_eng")]
    public string? CompanyNameEng { get; set; }

    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    [JsonPropertyName("floor")]
    public string? Floor { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("extra_name")]
    public string? ExtraName { get; set; }
}

// === Top Fluctuation ===

public class HmoneyTopFluctuationItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("change")]
    public decimal Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal ChangePercent { get; set; }

    [JsonPropertyName("floor_price")]
    public decimal FloorPrice { get; set; }

    [JsonPropertyName("ceiling_price")]
    public decimal CeilingPrice { get; set; }

    [JsonPropertyName("basic_price")]
    public decimal BasicPrice { get; set; }

    [JsonPropertyName("accumulated_vol")]
    public decimal AccumulatedVol { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }
}

// === Trading History Summary ===

public class HmoneyTradingHistorySummary
{
    [JsonPropertyName("change_day")]
    public decimal ChangeDay { get; set; }

    [JsonPropertyName("change_week")]
    public decimal ChangeWeek { get; set; }

    [JsonPropertyName("change_month")]
    public decimal ChangeMonth { get; set; }

    [JsonPropertyName("change_3_month")]
    public decimal Change3Month { get; set; }

    [JsonPropertyName("change_6_month")]
    public decimal Change6Month { get; set; }
}

// === Trading History (Daily) ===

public class HmoneyTradingHistoryItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = null!;

    [JsonPropertyName("trading_date")]
    public string? TradingDate { get; set; }

    [JsonPropertyName("match_price")]
    public decimal MatchPrice { get; set; }

    [JsonPropertyName("floor_price")]
    public decimal FloorPrice { get; set; }

    [JsonPropertyName("ceiling_price")]
    public decimal CeilingPrice { get; set; }

    [JsonPropertyName("basic_price")]
    public decimal BasicPrice { get; set; }

    [JsonPropertyName("accumulated_val")]
    public decimal AccumulatedVal { get; set; }

    [JsonPropertyName("accumulated_vol")]
    public decimal AccumulatedVol { get; set; }
}
