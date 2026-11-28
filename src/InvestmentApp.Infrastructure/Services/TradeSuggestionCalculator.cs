namespace InvestmentApp.Infrastructure.Services;

/// <summary>Gợi ý giao dịch (vào/cắt lỗ/mục tiêu) từ các mức hỗ trợ/kháng cự và ATR14. Hàm thuần.</summary>
public record TradeSuggestion(decimal Entry, decimal StopLoss, decimal Target, decimal? RiskReward, string StopLossMethod);

public static class TradeSuggestionCalculator
{
    public const string MethodSupport2 = "support2";
    public const string MethodAtr2x = "atr2x";
    public const string MethodAtr1x = "atr1x";
    public const string MethodPercent5 = "percent5";

    /// <summary>
    /// Vào lệnh = hỗ trợ gần nhất, mục tiêu = kháng cự gần nhất. Cắt lỗ = min(max(hỗ trợ thứ 2, vào − 2 × ATR14), vào − 1 × ATR14):
    /// mức CAO hơn (chặt hơn) nhưng khoảng cách tới giá vào nằm trong [1 ATR, 2 ATR]; sát hơn 1 ATR thì kéo ra 1 ATR (nhãn atr1x). Chỉ có một trong hai thì dùng cái đó; không có cả hai (không hỗ trợ 2, không ATR) thì −5% dưới
    /// hỗ trợ gần nhất như luật cũ. Cắt lỗ phải &gt; 0 và &lt; giá vào, nếu không thì không gợi ý (null).
    /// </summary>
    public static TradeSuggestion? Build(IReadOnlyList<decimal> supports, IReadOnlyList<decimal> resistances, decimal? atr14)
    {
        if (supports.Count == 0 || resistances.Count == 0) return null;

        var entry = supports[0];
        var target = resistances[0];
        decimal? second = supports.Count > 1 ? supports[1] : null;
        decimal? atrStop = atr14 is > 0m ? entry - 2m * atr14.Value : null;

        decimal stop;
        string method;
        if (second.HasValue && atrStop.HasValue)
        {
            stop = Math.Max(second.Value, atrStop.Value);
            method = second.Value >= atrStop.Value ? MethodSupport2 : MethodAtr2x;
        }
        else if (second.HasValue) { stop = second.Value; method = MethodSupport2; }
        else if (atrStop.HasValue) { stop = atrStop.Value; method = MethodAtr2x; }
        else { stop = entry * 0.95m; method = MethodPercent5; }

        // Khoảng cách cắt lỗ nằm trong [1 ATR, 2 ATR]: hỗ trợ 2 sát giá vào (< 1 ATR) cho cắt lỗ quá chặt và R:R ảo, nên bị kéo ra
        // tối thiểu 1 ATR. Chỉ khi có ATR; đúng 1 ATR thì giữ nguyên (nhãn vẫn là hỗ trợ).
        if (atr14 is > 0m)
        {
            var minDistanceStop = entry - atr14.Value;
            if (stop > minDistanceStop)
            {
                stop = minDistanceStop;
                method = MethodAtr1x;
            }
        }

        if (stop <= 0m || stop >= entry) return null;

        var risk = entry - stop;
        var reward = target - entry;
        return new TradeSuggestion(entry, stop, target, Math.Round(reward / risk, 1), method);
    }
}
