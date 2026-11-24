namespace InvestmentApp.Domain.Services;

/// <summary>Một mức ước lượng. Low/High và các tỉ lệ đều là phân số 0–1.</summary>
public sealed record CalibrationBucket(
    string Label, decimal Low, decimal High, int N,
    decimal? AvgEstimated, decimal? ActualWinRate, bool Sufficient);

/// <summary>
/// Độ khớp giữa % thắng đã ước lượng và kết quả thật. <see cref="Brier"/> và <see cref="OptimismPoints"/> chỉ có khi
/// <see cref="Sufficient"/> (tổng n ≥ <see cref="Calibration.MinTotalForSummary"/>); dưới ngưỡng là null, không phải 0.
/// </summary>
public sealed record CalibrationResult(
    int N, IReadOnlyList<CalibrationBucket> Buckets, decimal? Brier, decimal? OptimismPoints, bool Sufficient);

/// <summary>
/// Hàm thuần. Mẫu = (% thắng ước lượng 0–1, R thực tế). Lệnh hoà (|R| ≤ 0,1) bị loại: không phải thắng cũng không phải thua nên
/// không có "đã đoán đúng hay sai". Brier = trung bình (p − o)², o ∈ {0,1}; hữu hạn kể cả khi p = 0 hoặc 1.
/// Lệch lạc quan = trung bình p − tỉ lệ thắng thật, tính bằng điểm %.
/// </summary>
public static class Calibration
{
    public const int MinBucketSamples = 5;
    public const int MinTotalForSummary = 15;

    // Cận dưới bao gồm, cận trên loại trừ; mức cuối bao gồm cả 100%.
    private static readonly (string Label, decimal Low, decimal High)[] Bands =
    {
        ("0–40%", 0m, 0.40m),
        ("40–55%", 0.40m, 0.55m),
        ("55–70%", 0.55m, 0.70m),
        ("70–100%", 0.70m, 1.00m),
    };

    public static CalibrationResult Compute(IEnumerable<(decimal estimatedWinProbability, decimal realizedR)> samples)
    {
        var decisive = samples
            .Select(s => (P: Math.Clamp(s.estimatedWinProbability, 0m, 1m), Outcome: RMath.Classify(s.realizedR)))
            .Where(s => s.Outcome != TradeOutcome.Draw)
            .Select(s => (s.P, Won: s.Outcome == TradeOutcome.Win))
            .ToList();

        var buckets = new List<CalibrationBucket>();
        for (var i = 0; i < Bands.Length; i++)
        {
            var (label, low, high) = Bands[i];
            var last = i == Bands.Length - 1;
            var inBand = decisive.Where(s => s.P >= low && (last ? s.P <= high : s.P < high)).ToList();
            buckets.Add(new CalibrationBucket(
                label, low, high, inBand.Count,
                inBand.Count > 0 ? inBand.Average(s => s.P) : null,
                inBand.Count > 0 ? (decimal)inBand.Count(s => s.Won) / inBand.Count : null,
                inBand.Count >= MinBucketSamples));
        }

        var n = decisive.Count;
        var sufficient = n >= MinTotalForSummary;
        decimal? brier = null, optimism = null;
        if (sufficient)
        {
            brier = decisive.Average(s => { var o = s.Won ? 1m : 0m; return (s.P - o) * (s.P - o); });
            optimism = (decisive.Average(s => s.P) - (decimal)decisive.Count(s => s.Won) / n) * 100m;
        }

        return new CalibrationResult(n, buckets, brier, optimism, sufficient);
    }
}

/// <summary>Cỡ vị thế Kelly thận trọng từ dữ liệu R của chính người dùng.</summary>
public sealed record ConservativeKelly(
    int N, decimal? WinRateLower95, decimal? PayoffRatio, decimal? FullKelly, decimal? HalfKellyCapped, string? Reason);

/// <summary>
/// p = cận dưới Wilson 95% (không dùng tỉ lệ thắng thô: mẫu nhỏ thì tỉ lệ thô lạc quan), b = avgWinR / |avgLossR|,
/// f* = (b·p − (1 − p)) / b, rồi half-Kelly, trần 25% (cùng hằng số với mô hình Kelly hiện có).
/// Không đủ mẫu, chưa có lệnh thua (b không xác định) hoặc không có lợi thế thì không có cỡ vị thế và có <see cref="ConservativeKelly.Reason"/>.
/// </summary>
public static class KellyFromHistory
{
    public const int MinDecisiveTrades = 20;
    public const decimal Cap = 0.25m;

    public static ConservativeKelly Compute(int wins, int losses, decimal? avgWinR, decimal? avgLossR)
    {
        var n = wins + losses;
        if (n < MinDecisiveTrades)
            return new ConservativeKelly(n, null, null, null, null,
                $"Chưa đủ dữ liệu: cần tối thiểu {MinDecisiveTrades} lệnh thắng/thua, hiện có {n}.");

        var (lower, _) = ExpectancyCalculator.WilsonInterval(wins, n);

        // Không có lệnh thua: b = avgWin / avgLoss không xác định, không đoán.
        if (losses == 0 || avgLossR is null || avgLossR == 0m)
            return new ConservativeKelly(n, lower, null, null, null, "Chưa có lệnh thua để ước lượng.");

        // Mọi lệnh đều thua: không có lợi thế, không chia cho 0.
        if (wins == 0 || avgWinR is null || avgWinR <= 0m)
            return new ConservativeKelly(n, lower, null, 0m, 0m, NoEdge);

        var b = avgWinR.Value / Math.Abs(avgLossR.Value);
        var full = (b * lower - (1m - lower)) / b;
        if (full <= 0m)
            return new ConservativeKelly(n, lower, b, 0m, 0m, NoEdge);

        return new ConservativeKelly(n, lower, b, full, Math.Min(full / 2m, Cap), null);
    }

    public const string NoEdge = "Không có lợi thế theo dữ liệu — Kelly = 0.";
}
