using System.Text.Json;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

/// <summary>
/// Đọc payload UDF (TradingView) của 24hmoney: <c>{ s, t, o, h, l, c, v }</c>.
/// Giá tính bằng nghìn đồng nên nhân 1000 như <c>ScalePrice</c> của provider; <c>v</c> là số cổ phiếu
/// (khớp <c>accumulated_vol</c> của trading-history), không cần đổi đơn vị.
/// Hàm thuần: trả <c>null</c> khi payload không dùng được, để người gọi chuyển sang nguồn dự phòng.
/// </summary>
public static class TradingViewHistoryParser
{
    private const decimal PriceScale = 1000m;

    public static List<StockPriceData>? Parse(string? json, string symbol)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("s", out var status) || status.ValueKind != JsonValueKind.String
                || status.GetString() != "ok") return null;

            if (!TryArray(root, "t", out var t) || !TryArray(root, "o", out var o) || !TryArray(root, "h", out var h)
                || !TryArray(root, "l", out var l) || !TryArray(root, "c", out var c) || !TryArray(root, "v", out var v))
                return null;

            var n = t.GetArrayLength();
            if (n == 0 || o.GetArrayLength() != n || h.GetArrayLength() != n || l.GetArrayLength() != n
                || c.GetArrayLength() != n || v.GetArrayLength() != n)
                return null;

            var bars = new List<StockPriceData>(n);
            for (var i = 0; i < n; i++)
            {
                if (!Num(t[i], out var ts) || !Num(o[i], out var open) || !Num(h[i], out var high)
                    || !Num(l[i], out var low) || !Num(c[i], out var close) || close <= 0)
                    continue;

                Num(v[i], out var vol);   // thiếu khối lượng thì coi là 0, không bỏ nến
                bars.Add(new StockPriceData
                {
                    Symbol = symbol,
                    Date = DateTimeOffset.FromUnixTimeSeconds((long)ts).UtcDateTime.Date,
                    Open = (decimal)open * PriceScale,
                    High = (decimal)high * PriceScale,
                    Low = (decimal)low * PriceScale,
                    Close = (decimal)close * PriceScale,
                    Volume = (long)Math.Round(vol),
                    IsAdjusted = true
                });
            }

            return bars.Count == 0 ? null : bars.OrderBy(b => b.Date).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Nguồn trả đúng cấu trúc với <c>s = "no_data"</c>: mã không có dữ liệu, khác với bị chặn hay lỗi.</summary>
    public static bool IsNoData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("s", out var s)
                   && s.ValueKind == JsonValueKind.String && s.GetString() == "no_data";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Thân là một JSON object hợp lệ (dù <c>s</c> không phải ok): nguồn đang trả lời, không phải trang chặn.</summary>
    public static bool IsJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryArray(JsonElement root, string name, out JsonElement array)
    {
        if (root.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array) return true;
        array = default;
        return false;
    }

    private static bool Num(JsonElement e, out double value)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out value)) return true;
        value = 0;
        return false;
    }
}
