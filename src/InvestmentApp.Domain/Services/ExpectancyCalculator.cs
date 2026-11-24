namespace InvestmentApp.Domain.Services;

/// <summary>Thống kê kỳ vọng theo R của một tập lệnh đã đóng. Thiếu mẫu thì trường tương ứng là null, không phải 0.</summary>
public sealed record ExpectancyResult(
    int N, int Wins, int Losses, int Draws,
    decimal? WinRate, decimal? WinRateLow95, decimal? WinRateHigh95,
    decimal? AvgWinR, decimal? AvgLossR,
    decimal? ExpectancyR, decimal? ProfitFactorR);

/// <summary>
/// Hàm thuần: nhận R ròng của từng lệnh đã đóng, trả thống kê. Thắng/thua/hoà theo <see cref="RMath.Classify"/>.
/// <list type="bullet">
/// <item>% thắng = thắng / (thắng + thua) — lệnh hoà không vào mẫu số, hiện riêng.</item>
/// <item>Kỳ vọng R = trung bình R của MỌI lệnh, kể cả hoà (nếu không, lệnh hoà bị giấu làm kỳ vọng đẹp hơn thật).</item>
/// <item>PF theo R = tổng R thắng / |tổng R thua|; không có lệnh thua thì null (không dùng 999,99).</item>
/// <item>Khoảng tin cậy Wilson 95% của % thắng, tính trên số lệnh quyết định (thắng + thua).</item>
/// </list>
/// </summary>
public static class ExpectancyCalculator
{
    private const double Z95 = 1.959964;

    public static ExpectancyResult Compute(IEnumerable<decimal> realizedRs)
    {
        var rs = realizedRs.ToList();
        if (rs.Count == 0)
            return new ExpectancyResult(0, 0, 0, 0, null, null, null, null, null, null, null);

        var winsR = rs.Where(r => RMath.Classify(r) == TradeOutcome.Win).ToList();
        var lossesR = rs.Where(r => RMath.Classify(r) == TradeOutcome.Loss).ToList();
        var draws = rs.Count - winsR.Count - lossesR.Count;
        var decisive = winsR.Count + lossesR.Count;

        decimal? winRate = null, low = null, high = null;
        if (decisive > 0)
        {
            winRate = (decimal)winsR.Count / decisive;
            (low, high) = WilsonInterval(winsR.Count, decisive);
        }

        var totalWin = winsR.Sum();
        var totalLoss = Math.Abs(lossesR.Sum());

        return new ExpectancyResult(
            N: rs.Count, Wins: winsR.Count, Losses: lossesR.Count, Draws: draws,
            WinRate: winRate, WinRateLow95: low, WinRateHigh95: high,
            AvgWinR: winsR.Count > 0 ? winsR.Average() : null,
            AvgLossR: lossesR.Count > 0 ? lossesR.Average() : null,
            ExpectancyR: rs.Average(),
            ProfitFactorR: lossesR.Count > 0 ? totalWin / totalLoss : null);
    }

    /// <summary>Khoảng Wilson 95% cho tỉ lệ <paramref name="successes"/>/<paramref name="n"/> (n ≥ 1). Luôn hữu hạn, nằm trong [0, 1].</summary>
    public static (decimal Low, decimal High) WilsonInterval(int successes, int n)
    {
        double p = (double)successes / n;
        double z2 = Z95 * Z95;
        double denominator = 1 + z2 / n;
        double centre = (p + z2 / (2 * n)) / denominator;
        double half = Z95 * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / denominator;
        return ((decimal)Math.Max(0, centre - half), (decimal)Math.Min(1, centre + half));
    }
}
