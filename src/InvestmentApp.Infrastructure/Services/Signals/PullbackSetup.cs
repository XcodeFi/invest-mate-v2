namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Một phiên giá (đã điều chỉnh).</summary>
public sealed record Bar(DateTime Date, decimal Open, decimal High, decimal Low, decimal Close);

/// <summary>Các chuỗi chỉ báo theo từng phiên, cùng độ dài với chuỗi nến; null = chưa đủ dữ liệu khởi động.</summary>
public sealed record IndicatorSeries(
    IReadOnlyList<decimal?> Ema20, IReadOnlyList<decimal?> Ema50, IReadOnlyList<decimal?> Rsi14,
    IReadOnlyList<decimal?> Adx14, IReadOnlyList<decimal?> Atr14)
{
    /// <summary>Dựng bằng đúng các hàm của <see cref="IndicatorMath"/> mà biểu đồ và thẻ chỉ báo dùng (cùng giá trị, cùng làm tròn).</summary>
    public static IndicatorSeries Build(IReadOnlyList<Bar> bars)
    {
        var closes = bars.Select(b => b.Close).ToList();
        var highs = bars.Select(b => b.High).ToList();
        var lows = bars.Select(b => b.Low).ToList();
        return new IndicatorSeries(
            IndicatorMath.EmaSeries(closes, 20),
            IndicatorMath.EmaSeries(closes, 50),
            IndicatorMath.RsiSeriesFull(closes, 14),
            IndicatorMath.AdxSeries(highs, lows, closes, 14).adx,
            IndicatorMath.AtrSeries(highs, lows, closes, 14));
    }
}

/// <summary>
/// Setup Pullback xu hướng. Bối cảnh tại phiên t: EMA20 &gt; EMA50 và ADX14 ≥ 20. Kích hoạt: |Low − EMA20| ≤ ATR14, Close &gt; EMA20,
/// 40 ≤ RSI14 ≤ 55. Chỉ đếm PHIÊN ĐẦU TIÊN điều kiện đúng (phiên t−1 phải sai). Chỉ dùng dữ liệu tới phiên t. Luật cố định,
/// không chỉnh theo từng mã.
/// </summary>
public static class PullbackSetup
{
    /// <summary>Số phiên khởi động: tín hiệu chỉ hợp lệ từ chỉ số này (chỉ báo đã ổn định, mã mới lên sàn bị loại các phiên đầu).</summary>
    public const int WarmUp = 100;

    public static IReadOnlyList<int> FindSignals(IReadOnlyList<Bar> bars, IndicatorSeries s)
    {
        var signals = new List<int>();
        for (var t = WarmUp; t < bars.Count; t++)
            if (Holds(bars, s, t) && !Holds(bars, s, t - 1))
                signals.Add(t);
        return signals;
    }

    /// <summary>Bối cảnh và kích hoạt cùng đúng tại phiên i. Chỉ báo chưa sẵn sàng thì sai.</summary>
    internal static bool Holds(IReadOnlyList<Bar> bars, IndicatorSeries s, int i)
    {
        if (i < 0 || i >= bars.Count) return false;
        if (s.Ema20[i] is not { } ema20 || s.Ema50[i] is not { } ema50 || s.Rsi14[i] is not { } rsi
            || s.Adx14[i] is not { } adx || s.Atr14[i] is not { } atr)
            return false;

        var bar = bars[i];
        return ema20 > ema50 && adx >= 20m
            && Math.Abs(bar.Low - ema20) <= atr
            && bar.Close > ema20
            && rsi >= 40m && rsi <= 55m;
    }
}
