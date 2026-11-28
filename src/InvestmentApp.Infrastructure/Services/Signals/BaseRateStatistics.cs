using InvestmentApp.Domain.Services;

namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Một lệnh đã đóng của một mã trong rổ: ngày vào lệnh (để gom theo tháng) và R ròng.</summary>
public sealed record PooledTrade(DateTime EntryDate, decimal R);

/// <summary>
/// Tỉ lệ nền gộp trên rổ. Khoảng tin cậy là XẤP XỈ (bootstrap theo khối tháng), không phải khoảng Wilson/t trên các lệnh coi như độc lập.
/// <see cref="EffectiveMonths"/> = số tháng có ít nhất một lệnh; <see cref="TotalMonths"/> = số cụm dùng để lấy mẫu (gồm cả tháng không có lệnh).
/// </summary>
public sealed record BaseRateStats(
    int K, int N, int EffectiveMonths, int TotalMonths,
    decimal WinRate, decimal WinRateLow, decimal WinRateHigh,
    decimal EvR, decimal EvRLow, decimal EvRHigh);

public static class BaseRateStatistics
{
    /// <summary>Số mã tối thiểu có dữ liệu dùng được.</summary>
    public const int MinSymbols = 15;

    /// <summary>Số lệnh đã đóng tối thiểu.</summary>
    public const int MinTrades = 30;

    /// <summary>Số tháng có lệnh tối thiểu: ít hơn thì "mẫu" chỉ là vài bối cảnh thị trường, bootstrap theo tháng không còn ý nghĩa.</summary>
    public const int MinEffectiveMonths = 24;

    /// <summary>Số lần lấy mẫu lại.</summary>
    public const int Resamples = 2000;

    /// <summary>Hạt giống cố định: cùng dữ liệu luôn ra cùng khoảng (kết quả được nhớ cả ngày và so sánh được giữa các lần chạy).</summary>
    public const ulong Seed = 20_261_128UL;

    /// <summary>Lý do chưa đủ dữ liệu (null = đủ). Số liệu k/30 để người đọc thấy cách xa ngưỡng bao nhiêu.</summary>
    public static string? GateFailure(int k, int n, int effectiveMonths)
    {
        if (k < MinSymbols) return $"Chưa đủ mã VN30 có dữ liệu ({k}/30, cần từ {MinSymbols})";
        if (n < MinTrades) return $"Chưa đủ lệnh đã đóng trên rổ VN30 ({n}, cần từ {MinTrades})";
        if (effectiveMonths < MinEffectiveMonths) return $"Các lệnh mới rải trên {effectiveMonths} tháng, cần từ {MinEffectiveMonths} tháng";
        return null;
    }

    public static int CountEffectiveMonths(IReadOnlyList<PooledTrade> trades)
        => trades.Select(t => MonthKey(t.EntryDate)).Distinct().Count();

    /// <summary>
    /// null khi chưa qua cổng (k, n, số tháng). Ngược lại: tỉ lệ thắng (R &gt; 0) và R trung bình của TOÀN BỘ lệnh, kèm khoảng 95% xấp xỉ
    /// từ bootstrap theo khối tháng: lấy mẫu lại các THÁNG (có hoàn lại, tháng không có lệnh vẫn là một cụm), mỗi lần tính lại tỉ lệ
    /// thắng và R trung bình trên mọi lệnh của các tháng được chọn (ước lượng tỉ số), rồi lấy phân vị 2,5 và 97,5.
    /// Các lệnh cùng tháng luôn đi cùng nhau vì chúng chịu chung bối cảnh thị trường.
    /// </summary>
    public static BaseRateStats? Compute(int k, IReadOnlyList<PooledTrade> trades)
    {
        var n = trades.Count;
        var effective = CountEffectiveMonths(trades);
        if (GateFailure(k, n, effective) != null) return null;

        var first = trades.Min(t => MonthKey(t.EntryDate));
        var last = trades.Max(t => MonthKey(t.EntryDate));
        var total = last - first + 1;

        var count = new int[total];
        var wins = new int[total];
        var sum = new double[total];
        foreach (var t in trades)
        {
            var m = MonthKey(t.EntryDate) - first;
            count[m]++;
            if (t.R > 0m) wins[m]++;
            sum[m] += (double)t.R;
        }

        var winRate = (double)wins.Sum() / n;
        var evR = sum.Sum() / n;

        var rng = new SplitMix64(Seed);
        var winSamples = new List<double>(Resamples);
        var evSamples = new List<double>(Resamples);
        for (var b = 0; b < Resamples; b++)
        {
            long c = 0, w = 0;
            double s = 0;
            for (var i = 0; i < total; i++)
            {
                var m = (int)(rng.Next() % (ulong)total);
                c += count[m];
                w += wins[m];
                s += sum[m];
            }
            if (c == 0) continue;   // lần lấy mẫu rơi toàn tháng trống: không có tỉ số
            winSamples.Add((double)w / c);
            evSamples.Add(s / c);
        }

        var (wLow, wHigh) = Percentiles(winSamples);
        var (eLow, eHigh) = Percentiles(evSamples);

        return new BaseRateStats(k, n, effective, total,
            R(winRate), R(wLow), R(wHigh), R(evR), R(eLow), R(eHigh));
    }

    private static decimal R(double x) => Math.Round((decimal)x, 4);

    private static int MonthKey(DateTime d) => d.Year * 12 + (d.Month - 1);

    private static (double Low, double High) Percentiles(List<double> samples)
    {
        samples.Sort();
        var last = samples.Count - 1;
        return (samples[(int)Math.Round(0.025 * last)], samples[(int)Math.Round(0.975 * last)]);
    }

    /// <summary>Bộ sinh số giả ngẫu nhiên tự viết: kết quả không đổi giữa các phiên bản .NET (System.Random có hạt giống không được bảo đảm như vậy).</summary>
    private sealed class SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public ulong Next()
        {
            var z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
