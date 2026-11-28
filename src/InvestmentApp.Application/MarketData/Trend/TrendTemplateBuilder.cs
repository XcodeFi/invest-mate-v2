namespace InvestmentApp.Application.MarketData.Trend;

/// <summary>Dựng thẻ Trend Template (Minervini) từ chuỗi đóng/cao/thấp theo ngày. Hàm THUẦN; chỉ SMA, không EMA, theo đúng quy tắc gốc.</summary>
public static class TrendTemplateBuilder
{
    /// <summary>Số phiên của "1 tháng" cho tiêu chí SMA200 dốc lên.</summary>
    private const int MonthSessions = 21;
    private const string NoData = "Không đủ dữ liệu";

    public static TrendTemplateInfo Build(IReadOnlyList<decimal> closes, IReadOnlyList<decimal> highs, IReadOnlyList<decimal> lows)
    {
        var n = closes.Count;
        decimal? price = n > 0 ? closes[n - 1] : null;
        var sma50 = Last(PriceStructure.Sma(closes, 50));
        var sma150 = Last(PriceStructure.Sma(closes, 150));
        var smaSeries200 = PriceStructure.Sma(closes, 200);
        var sma200 = Last(smaSeries200);
        var sma200Ago = n > MonthSessions ? smaSeries200[n - 1 - MonthSessions] : null;
        var (high52, low52) = PriceStructure.Week52(highs, lows);

        var rows = new List<TrendTemplateCriterion>
        {
            Above("price_above_sma150", "Giá nằm trên SMA150", price, sma150, "SMA150 cần 150 phiên"),
            Above("price_above_sma200", "Giá nằm trên SMA200", price, sma200, "SMA200 cần 200 phiên"),
            Above("sma150_above_sma200", "SMA150 nằm trên SMA200", sma150, sma200, "SMA200 cần 200 phiên"),
            Above("sma200_rising", "SMA200 dốc lên ít nhất 1 tháng (21 phiên)", sma200, sma200Ago, "cần khoảng 221 phiên"),
            Above("sma50_above_sma150", "SMA50 nằm trên SMA150", sma50, sma150, "SMA150 cần 150 phiên"),
            Above("sma50_above_sma200", "SMA50 nằm trên SMA200", sma50, sma200, "SMA200 cần 200 phiên"),
            Above("price_above_sma50", "Giá nằm trên SMA50", price, sma50, "SMA50 cần 50 phiên"),
            Threshold("above_52w_low", "Giá cao hơn đáy 52 tuần ít nhất 30%", Pct(price, low52), 30m, atLeast: true, "52 tuần cần 250 phiên"),
            Threshold("near_52w_high", "Giá cách đỉnh 52 tuần không quá 25%", BelowPct(high52, price), 25m, atLeast: false, "52 tuần cần 250 phiên"),
            new TrendTemplateCriterion
            {
                Key = "rs_rating",
                Rule = "Xếp hạng sức mạnh tương đối (RS) từ 70 trở lên (ưu tiên 80+)",
                Detail = "Ứng dụng chưa có xếp hạng RS theo chuẩn IBD nên không tính tiêu chí này; xếp hạng phân vị của bộ sàng lọc là thước đo khác.",
            },
        };

        return new TrendTemplateInfo
        {
            Sessions = n,
            Criteria = rows,
            Note = "Đây là bộ lọc theo xu hướng, không phải tín hiệu mua; mỗi dòng đứng riêng và không có kết luận chung.",
        };
    }

    private static decimal? Last(List<decimal?> series) => series.Count > 0 ? series[^1] : null;

    /// <summary>(a / b − 1) × 100 CHƯA làm tròn (đạt/không đạt quyết định trên số này, làm tròn chỉ để hiển thị); null khi thiếu một trong hai hoặc b = 0.</summary>
    private static decimal? Pct(decimal? a, decimal? b) =>
        a.HasValue && b.HasValue && b.Value != 0 ? (a.Value / b.Value - 1) * 100 : null;

    /// <summary>(đỉnh − giá) / đỉnh × 100 chưa làm tròn: giá thấp hơn đỉnh bao nhiêu %.</summary>
    private static decimal? BelowPct(decimal? high, decimal? price) =>
        high.HasValue && price.HasValue && high.Value != 0 ? (high.Value - price.Value) / high.Value * 100 : null;

    private static decimal? Show(decimal? raw) => raw.HasValue ? Math.Round(raw.Value, 2) : null;

    private static TrendTemplateCriterion Above(string key, string rule, decimal? a, decimal? b, string needs)
    {
        var raw = Pct(a, b);
        return new TrendTemplateCriterion
        {
            Key = key, Rule = rule, Value = Show(raw), Unit = "%", Threshold = 0m,
            Met = raw.HasValue ? raw.Value > 0 : null,
            Detail = raw.HasValue ? null : $"{NoData} ({needs}).",
        };
    }

    private static TrendTemplateCriterion Threshold(string key, string rule, decimal? raw, decimal threshold, bool atLeast, string needs) => new()
    {
        Key = key, Rule = rule, Value = Show(raw), Unit = "%", Threshold = threshold,
        Met = raw.HasValue ? (atLeast ? raw.Value >= threshold : raw.Value <= threshold) : null,
        Detail = raw.HasValue ? null : $"{NoData} ({needs}).",
    };

    /// <summary>
    /// Chuỗi chưa điều chỉnh theo sự kiện quyền: không tính gì (ngày chia tách hoặc thưởng cổ phiếu là một cú tụt giả làm sai mọi mức),
    /// mọi dòng không có số và không có kết luận đạt hay không.
    /// </summary>
    public static TrendTemplateInfo Unadjusted(int sessions)
    {
        var info = Build(new List<decimal>(), new List<decimal>(), new List<decimal>());
        info.Sessions = sessions;
        foreach (var row in info.Criteria.Where(r => r.Key != "rs_rating"))
            row.Detail = "Giá chưa điều chỉnh theo sự kiện quyền nên không tính (ngày chia tách hoặc thưởng cổ phiếu là một cú tụt giả).";
        return info;
    }
}
