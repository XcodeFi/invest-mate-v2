using InvestmentApp.Domain.Services;

namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Thống kê một setup trên các lệnh đã đóng. Dưới <see cref="MinSample"/> lệnh thì chỉ có số lượng, mọi tỉ lệ đều null.</summary>
public sealed record SetupStats(
    int N, bool Sufficient,
    decimal? WinRate, decimal? WinRateLow, decimal? WinRateHigh,
    decimal? EvR, decimal? EvRLow, decimal? EvRHigh,
    decimal? AvgWinR, decimal? AvgLossR);

public static class SetupStatistics
{
    /// <summary>Số lệnh đã đóng tối thiểu để hiện tỉ lệ thắng và EV.</summary>
    public const int MinSample = 30;

    /// <summary>
    /// Thắng = R ròng &gt; 0; R ≤ 0 là thua (kể cả hoà vốn sau chi phí). Tỉ lệ thắng kèm khoảng Wilson 95% (dùng lại
    /// <see cref="ExpectancyCalculator.WilsonInterval"/>). EV theo R kèm khoảng t 95%.
    /// </summary>
    public static SetupStats Compute(IReadOnlyList<decimal> rs)
    {
        var n = rs.Count;
        if (n < MinSample)
            return new SetupStats(n, false, null, null, null, null, null, null, null, null);

        var wins = rs.Where(r => r > 0m).ToList();
        var losses = rs.Where(r => r <= 0m).ToList();
        var (wLow, wHigh) = ExpectancyCalculator.WilsonInterval(wins.Count, n);
        var ev = rs.Average();
        var interval = EvInterval(rs);

        return new SetupStats(
            n, true,
            (decimal)wins.Count / n, wLow, wHigh,
            ev, interval?.Low, interval?.High,
            wins.Count > 0 ? wins.Average() : null,
            losses.Count > 0 ? losses.Average() : null);
    }

    /// <summary>
    /// Khoảng tin cậy 95% của R trung bình: mean ± t(0,975; n−1) × sd/√n. Dùng phân phối t, không phải chuẩn, vì độ lệch chuẩn được
    /// ước lượng từ chính mẫu nhỏ (n vài chục): khoảng chuẩn quá hẹp ở n = 30 (1,96 so với 2,045). Không xác định (null) khi n &lt; 2 hoặc
    /// sd = 0 (mọi lệnh cùng R): không báo một khoảng rộng 0 như thể chắc chắn.
    /// </summary>
    public static (decimal Low, decimal High)? EvInterval(IReadOnlyList<decimal> rs)
    {
        var n = rs.Count;
        if (n < 2) return null;

        var mean = rs.Average();
        var variance = rs.Sum(r => (r - mean) * (r - mean)) / (n - 1);
        if (variance <= 0m) return null;

        var sd = (decimal)Math.Sqrt((double)variance);
        var half = TQuantile975(n - 1) * sd / (decimal)Math.Sqrt(n);
        return (mean - half, mean + half);
    }

    // t(0,975; df) cho df 1..30
    private static readonly double[] T975 =
    {
        12.7062, 4.3027, 3.1824, 2.7764, 2.5706, 2.4469, 2.3646, 2.3060, 2.2622, 2.2281,
        2.2010, 2.1788, 2.1604, 2.1448, 2.1314, 2.1199, 2.1098, 2.1009, 2.0930, 2.0860,
        2.0796, 2.0739, 2.0687, 2.0639, 2.0595, 2.0555, 2.0518, 2.0484, 2.0452, 2.0423
    };

    /// <summary>Phân vị 97,5% của phân phối t với <paramref name="df"/> bậc tự do: bảng tới 30, sau đó khai triển Cornish–Fisher quanh z = 1,95996.</summary>
    public static decimal TQuantile975(int df)
    {
        if (df < 1) throw new ArgumentOutOfRangeException(nameof(df));
        if (df <= 30) return (decimal)T975[df - 1];

        const double z = 1.959964;
        var z3 = z * z * z;
        var z5 = z3 * z * z;
        var t = z
            + (z3 + z) / (4.0 * df)
            + (5 * z5 + 16 * z3 + 3 * z) / (96.0 * df * df);
        return (decimal)t;
    }
}
