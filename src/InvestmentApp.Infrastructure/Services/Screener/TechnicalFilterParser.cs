using System.Text.Json;
using InvestmentApp.Application.MarketData.Screener;

namespace InvestmentApp.Infrastructure.Services.Screener;

/// <summary>Một trang technical-filter đã đọc: các dòng hợp lệ cùng số đo cho cổng chất lượng.</summary>
public sealed record TechnicalFilterPage(int TotalPages, IReadOnlyList<ScreenerRow> Rows, int RawCount, int InvalidCount, int RsOutOfRangeCount);

/// <summary>
/// Đọc phản hồi <c>/v1/ios/company/technical-filter</c> của 24hmoney. Đơn vị (đo 2026-11-28, ghim bằng test mẫu thật):
/// giá (<c>match_price</c>, <c>match_price_10</c>) tính bằng NGHÌN đồng; <c>market_cap</c> và <c>profit_after_tax</c> bằng TỶ đồng;
/// <c>avg_trading_vol_10</c> bằng cổ phiếu; <c>rs1m/rs3m/rs52w</c> là XẾP HẠNG PHÂN VỊ 1–99 trên toàn thị trường (không phải tỉ suất
/// sinh lời), null khi thiếu; <c>the_beta</c> = 0 là thiếu dữ liệu (223/1827 dòng). Không dùng <c>eps4Q</c> (không nhất quán trong mẫu).
/// </summary>
public static class TechnicalFilterParser
{
    /// <summary>Tham số rỗng cho ra data rỗng; bộ lọc cho phép này trả toàn thị trường (đo 2026-11-28: 1827 mã, 4 trang × 500).</summary>
    public const string FullUniverseParam = "market_cap:0:999999999|";

    public const int PageSize = 500;
    private const decimal PriceScale = 1000m;
    private const decimal MaxPlausibleCapBn = 5_000_000m;
    private static readonly string[] Floors = { "HOSE", "HNX", "UPCOM" };

    public static TechnicalFilterPage Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { throw new FormatException("technical-filter: phản hồi không phải JSON."); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Number || status.GetInt32() != 200
                || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("data", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new FormatException("technical-filter: phản hồi không đúng dạng mong đợi (status 200 với data.data là mảng).");

            var totalPages = data.TryGetProperty("total_page", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 1;
            var rows = new List<ScreenerRow>();
            var invalid = 0;
            var rsOut = 0;
            var raw = 0;
            foreach (var item in list.EnumerateArray())
            {
                raw++;
                var rs1 = Rank(item, "rs1m", ref rsOut);
                var rs3 = Rank(item, "rs3m", ref rsOut);
                var rs52 = Rank(item, "rs52w", ref rsOut);

                var symbol = Str(item, "symbol")?.Trim().ToUpperInvariant();
                var floor = Str(item, "floor")?.Trim().ToUpperInvariant();
                var cap = Num(item, "market_cap");
                var price = Num(item, "match_price") * PriceScale;
                if (string.IsNullOrEmpty(symbol) || floor == null || !Floors.Contains(floor)
                    || cap is not > 0 || cap > MaxPlausibleCapBn || price is not > 0)
                {
                    invalid++;
                    continue;
                }

                var price10 = Num(item, "match_price_10") * PriceScale;
                var beta = Num(item, "the_beta");
                rows.Add(new ScreenerRow(
                    symbol, floor, Str(item, "icb_group"), cap.Value, price.Value,
                    Num(item, "avg_trading_vol_10"), price10 is > 0 ? price10 : null,
                    Num(item, "pe4Q"), Num(item, "pb4Q"), Num(item, "roe"), Num(item, "roa"), Num(item, "profit_after_tax"),
                    rs1, rs3, rs52, beta is null or 0 ? null : beta));
            }
            return new TechnicalFilterPage(totalPages, rows, raw, invalid, rsOut);
        }
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Num(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;

    /// <summary>Xếp hạng 1–99; null/thiếu giữ là null. Giá trị ngoài khoảng được đếm để cổng chất lượng huỷ cả lần làm mới.</summary>
    private static int? Rank(JsonElement e, string name, ref int outOfRange)
    {
        var d = Num(e, name);
        if (d is null) return null;
        var rounded = (int)Math.Round(d.Value, MidpointRounding.AwayFromZero);
        if (rounded < 1 || rounded > 99) { outOfRange++; return null; }
        return rounded;
    }
}
