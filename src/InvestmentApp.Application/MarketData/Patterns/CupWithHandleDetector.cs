using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Patterns;

/// <summary>
/// Dò mô hình Cốc tay cầm trên chuỗi giá ngày đã điều chỉnh. Hàm THUẦN: cùng đầu vào cho cùng kết quả, không đọc đồng hồ, không đổi đầu vào.
/// Duyệt các đỉnh trái từ gần nhất về xa nhất; với mỗi đỉnh lấy đáy thấp nhất sau nó, vành phải là đỉnh cao nhất sau đáy (không tính nến cuối),
/// tay cầm là phần từ vành phải tới nến cuối. Ứng viên đầu tiên thoả MỌI điều kiện bắt buộc được trả về (tối đa một ứng viên); không có thì null.
/// Không điểm số, không xác suất.
/// </summary>
public static class CupWithHandleDetector
{
    private const int SessionsPerWeek = 5;
    private const int VolumeAverageSessions = 50;

    public static PatternCandidate? Detect(IReadOnlyList<StockPriceData> input, CupWithHandleOptions? options = null)
    {
        var o = options ?? new CupWithHandleOptions();
        var bars = input.OrderBy(b => b.Date).ToList();
        var n = bars.Count;
        if (n < o.MinBars) return null;

        var last = bars[n - 1];
        // Cửa sổ tìm: cốc dài nhất + tay cầm dài nhất + một đoạn đệm.
        var lookback = (int)o.MaxCupWeeks * SessionsPerWeek + o.MaxHandleSessions + o.SwingWindow;
        var first = Math.Max(0, n - lookback);

        for (var l = n - 1 - o.MinHandleSessions - o.SwingWindow; l >= first + o.SwingWindow; l--)
        {
            if (!IsSwingHigh(bars, l, o.SwingWindow)) continue;

            // Đáy: thấp nhất sau đỉnh trái, bỏ ra các phiên của tay cầm tối thiểu ở cuối.
            var b = ArgMinLow(bars, l + 1, n - 1 - o.MinHandleSessions);
            if (b < 0 || b - l < o.SwingWindow) continue;

            // Vành phải: cao nhất sau đáy, không tính nến cuối (nến cuối có thể là nến phá vỡ).
            var r = ArgMaxHigh(bars, b + 1, n - 2);
            if (r < 0 || r - b < o.SwingWindow) continue;

            var handleLength = n - 1 - r;
            if (handleLength < o.MinHandleSessions || handleLength > o.MaxHandleSessions) continue;

            var leftHigh = bars[l].High;
            var bottomLow = bars[b].Low;
            var rimHigh = bars[r].High;
            if (leftHigh <= 0 || rimHigh <= 0) continue;
            if (rimHigh > leftHigh * 1.05m) continue;   // vành phải cao hơn hẳn đỉnh trái: đó là cấu trúc khác, không phải vành của cốc này

            var handleLow = MinLow(bars, r + 1, n - 2);   // không tính nến cuối: nến cuối được so với đáy này
            var cupWeeks = (decimal)(r - l) / SessionsPerWeek;
            var cupDepth = (leftHigh - bottomLow) / leftHigh * 100m;
            var handleDepth = (rimHigh - handleLow) / rimHigh * 100m;
            var pivot = rimHigh;
            var close = last.Close;
            var distance = (pivot - close) / pivot * 100m;

            var criteria = new List<PatternCriterion>
            {
                Crit("cup-length", $"Cốc dài {o.MinCupWeeks:0.#}–{o.MaxCupWeeks:0.#} tuần", cupWeeks, "tuần", cupWeeks >= o.MinCupWeeks && cupWeeks <= o.MaxCupWeeks),
                Crit("cup-depth", $"Cốc sâu {o.MinCupDepthPct:0.#}–{o.MaxCupDepthPct:0.#}% so với đỉnh trái", cupDepth, "%", cupDepth >= o.MinCupDepthPct && cupDepth <= o.MaxCupDepthPct),
                Crit("rim-recovery", $"Vành phải hồi ≥ {o.MinRimRecovery * 100:0.#}% đỉnh trái", rimHigh / leftHigh * 100m, "%", rimHigh >= leftHigh * o.MinRimRecovery),
                Crit("handle-length", $"Tay cầm dài {o.MinHandleSessions}–{o.MaxHandleSessions} phiên", handleLength, "phiên", true),
                Crit("handle-depth", $"Tay cầm sâu {o.MinHandleDepthPct:0.#}–{o.MaxHandleDepthPct:0.#}% so với vành phải", handleDepth, "%", handleDepth >= o.MinHandleDepthPct && handleDepth <= o.MaxHandleDepthPct),
                Crit("handle-upper-half", "Đáy tay cầm nằm nửa trên của cốc", handleLow, "giá", handleLow >= bottomLow + (rimHigh - bottomLow) / 2m),
                Crit("above-handle-low", "Giá đóng cửa chưa thủng đáy tay cầm", close, "giá", close >= handleLow),
                Crit("not-extended", $"Giá chưa vượt pivot quá {o.MaxExtendedPct:0.#}%", -distance, "%", distance >= -o.MaxExtendedPct),
            };
            if (criteria.Any(k => k.Required && !k.Passed)) continue;

            var volumeRatio = VolumeRatio(bars, VolumeAverageSessions);
            var state = close > pivot
                ? (volumeRatio >= o.BreakoutVolumeMultiple ? "broke-out-with-volume" : "broke-out-unconfirmed")
                : (distance <= o.NearPivotPct ? "near-pivot" : "forming");
            criteria.Add(new PatternCriterion
            {
                Key = "breakout-volume", Rule = $"Khối lượng nến cuối ≥ {o.BreakoutVolumeMultiple:0.#}× trung bình 50 phiên (chỉ xét khi đã vượt pivot)",
                Value = volumeRatio, Unit = "×", Passed = volumeRatio >= o.BreakoutVolumeMultiple, Required = false
            });

            var stop = handleLow * o.StopBelowHandleFactor;
            var cupDepthAbs = leftHigh - bottomLow;
            var target = pivot + cupDepthAbs;
            var risk = pivot - stop;

            return new PatternCandidate
            {
                State = state,
                StateVi = StateVi(state),
                StartDate = bars[l].Date,
                BottomDate = bars[b].Date,
                RightLipDate = bars[r].Date,
                CupLengthWeeks = Math.Round(cupWeeks, 1),
                CupDepthPct = Math.Round(cupDepth, 1),
                HandleLengthSessions = handleLength,
                HandleDepthPct = Math.Round(handleDepth, 1),
                PivotPrice = pivot,
                DistanceToPivotPct = Math.Round(distance, 1),
                VolumeVsAvg50 = volumeRatio.HasValue ? Math.Round(volumeRatio.Value, 2) : null,
                StopPrice = stop,
                RiskPct = Math.Round(risk / pivot * 100m, 1),
                TargetPrice = target,
                RewardPct = Math.Round(cupDepthAbs / pivot * 100m, 1),
                RewardRisk = risk > 0 ? Math.Round(cupDepthAbs / risk, 1) : 0m,
                Criteria = criteria
            };
        }
        return null;
    }

    private static PatternCriterion Crit(string key, string rule, decimal value, string unit, bool passed) => new()
    {
        Key = key, Rule = rule, Value = Math.Round(value, 2), Unit = unit, Passed = passed, Required = true
    };

    private static string StateVi(string state) => state switch
    {
        "near-pivot" => "Sát pivot",
        "broke-out-unconfirmed" => "Vượt pivot, chưa có khối lượng xác nhận",
        "broke-out-with-volume" => "Vượt pivot kèm khối lượng",
        _ => "Đang hình thành"
    };

    private static bool IsSwingHigh(List<StockPriceData> bars, int i, int window)
    {
        for (var k = Math.Max(0, i - window); k <= Math.Min(bars.Count - 1, i + window); k++)
            if (bars[k].High > bars[i].High) return false;
        return true;
    }

    /// <summary>Chỉ số có đáy thấp nhất trong [from, to] (lần xuất hiện đầu tiên); -1 nếu khoảng rỗng.</summary>
    private static int ArgMinLow(List<StockPriceData> bars, int from, int to)
    {
        var best = -1;
        for (var k = from; k <= to; k++)
            if (best < 0 || bars[k].Low < bars[best].Low) best = k;
        return best;
    }

    /// <summary>Chỉ số có đỉnh cao nhất trong [from, to] (lần xuất hiện cuối cùng, để tay cầm không dài hơn thực tế); -1 nếu khoảng rỗng.</summary>
    private static int ArgMaxHigh(List<StockPriceData> bars, int from, int to)
    {
        var best = -1;
        for (var k = from; k <= to; k++)
            if (best < 0 || bars[k].High >= bars[best].High) best = k;
        return best;
    }

    private static decimal MinLow(List<StockPriceData> bars, int from, int to)
    {
        var min = decimal.MaxValue;
        for (var k = from; k <= to; k++) min = Math.Min(min, bars[k].Low);
        return min == decimal.MaxValue ? bars[^1].Low : min;
    }

    private static decimal? VolumeRatio(List<StockPriceData> bars, int sessions)
    {
        var n = bars.Count;
        if (n < sessions + 1) return null;
        var avg = bars.Skip(n - 1 - sessions).Take(sessions).Average(b => (decimal)b.Volume);
        return avg > 0 ? bars[n - 1].Volume / avg : null;
    }
}
