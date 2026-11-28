namespace InvestmentApp.Application.MarketData.Trend;

/// <summary>
/// Đường trung bình đơn giản, kênh Donchian và đỉnh/đáy 52 tuần trên chuỗi ngày. Hàm THUẦN. Chưa đủ phiên thì null, không bao giờ 0.
/// </summary>
public static class PriceStructure
{
    /// <summary>Số phiên của "52 tuần": khoảng 250 phiên giao dịch (thị trường Việt Nam nghỉ cuối tuần và lễ).</summary>
    public const int Week52Sessions = 250;

    /// <summary>Trung bình đơn giản (SMA) của <paramref name="period"/> phiên gần nhất tại mỗi phiên; null cho tới khi đủ phiên.</summary>
    public static List<decimal?> Sma(IReadOnlyList<decimal> values, int period)
    {
        var result = new List<decimal?>(values.Count);
        decimal sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period) sum -= values[i - period];
            result.Add(i >= period - 1 ? sum / period : null);
        }
        return result;
    }

    /// <summary>
    /// Đỉnh cao nhất của <paramref name="n"/> phiên TRƯỚC phiên hiện tại (không tính phiên hiện tại), theo cách Turtle xét phá vỡ:
    /// giá vượt mức này mới là phá vỡ. Null cho tới khi có đủ <paramref name="n"/> phiên trước đó.
    /// </summary>
    public static List<decimal?> PriorHighest(IReadOnlyList<decimal> highs, int n) => Prior(highs, n, Math.Max);

    /// <summary>Đáy thấp nhất của <paramref name="n"/> phiên trước phiên hiện tại (không tính phiên hiện tại): giá thủng mức này là tín hiệu thoát của Turtle.</summary>
    public static List<decimal?> PriorLowest(IReadOnlyList<decimal> lows, int n) => Prior(lows, n, Math.Min);

    private static List<decimal?> Prior(IReadOnlyList<decimal> values, int n, Func<decimal, decimal, decimal> pick)
    {
        var result = new List<decimal?>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (i < n) { result.Add(null); continue; }
            var best = values[i - n];
            for (var k = i - n + 1; k < i; k++) best = pick(best, values[k]);
            result.Add(best);
        }
        return result;
    }

    /// <summary>Đỉnh cao nhất và đáy thấp nhất của 250 phiên gần nhất (kể cả phiên hiện tại); cả hai null khi chưa đủ 250 phiên.</summary>
    public static (decimal? High, decimal? Low) Week52(IReadOnlyList<decimal> highs, IReadOnlyList<decimal> lows)
    {
        if (highs.Count < Week52Sessions || lows.Count < Week52Sessions) return (null, null);
        return (highs.Skip(highs.Count - Week52Sessions).Max(), lows.Skip(lows.Count - Week52Sessions).Min());
    }
}
