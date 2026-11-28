using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Patterns;

namespace InvestmentApp.Application.Tests.MarketData.Patterns;

/// <summary>Bộ dò mô hình Cốc tay cầm: hàm thuần trên nến tổng hợp. Mỗi ca dựng chuỗi từ các đoạn thẳng nên số đo biết trước.</summary>
public class CupWithHandleDetectorTests
{
    private static readonly DateTime Day0 = new(2025, 1, 1);

    /// <summary>Chuỗi nến từ các đoạn (số phiên, giá đóng cuối đoạn) nối tiếp; cao = đóng×1,004, thấp = đóng×0,996, khối lượng cố định trừ nến cuối.</summary>
    private static List<StockPriceData> Build(decimal start, (int Sessions, decimal End)[] legs, long volume = 1_000_000, long? lastVolume = null)
    {
        var bars = new List<StockPriceData>();
        var price = start;
        foreach (var (sessions, end) in legs)
        {
            for (var k = 1; k <= sessions; k++)
            {
                var close = price + (end - price) * k / sessions;
                bars.Add(Bar(bars.Count, close, volume));
            }
            price = end;
        }
        if (lastVolume.HasValue) bars[^1].Volume = lastVolume.Value;
        return bars;
    }

    private static StockPriceData Bar(int i, decimal close, long volume) => new()
    {
        Symbol = "TST", Date = Day0.AddDays(i), Open = close, High = close * 1.004m, Low = close * 0.996m, Close = close, Volume = volume, IsAdjusted = true
    };

    /// <summary>Cốc sạch: đà tăng trước đó, đỉnh trái 100, đáy <paramref name="bottom"/>, vành phải 98, tay cầm xuống 90 rồi hồi tới <paramref name="lastClose"/>.</summary>
    private static List<StockPriceData> CleanCup(decimal lastClose = 96m, decimal bottom = 72m, decimal handleLow = 90m, long? lastVolume = null,
        int warmUp = 70, int down = 25, int up = 40)
        => Build(60m, new[] { (warmUp, 100m), (down, bottom), (up, 98m), (7, handleLow), (3, lastClose) }, lastVolume: lastVolume);

    [Fact]
    public void a_clean_cup_with_handle_near_the_pivot_is_found_with_its_measurements()
    {
        var bars = CleanCup(lastClose: 96m);

        var c = CupWithHandleDetector.Detect(bars);

        c.Should().NotBeNull();
        c!.Type.Should().Be("cup-with-handle");
        c.State.Should().Be("near-pivot");
        c.CupLengthWeeks.Should().Be(13m);
        c.CupDepthPct.Should().BeApproximately(28.6m, 0.2m);
        c.HandleLengthSessions.Should().Be(10);
        c.HandleDepthPct.Should().BeApproximately(8.9m, 0.2m);
        c.PivotPrice.Should().BeApproximately(98.392m, 0.01m);
        c.DistanceToPivotPct.Should().BeApproximately(2.4m, 0.2m);
        c.StartDate.Should().Be(bars[69].Date);
        c.BottomDate.Should().Be(bars[94].Date);
        c.RightLipDate.Should().Be(bars[134].Date);
        c.Criteria.Where(k => k.Required).Should().OnlyContain(k => k.Passed);
    }

    [Fact]
    public void a_cup_still_far_below_the_pivot_is_only_forming()
    {
        var c = CupWithHandleDetector.Detect(CleanCup(lastClose: 90.5m));

        c!.State.Should().Be("forming");
        c.DistanceToPivotPct.Should().BeGreaterThan(3m);
    }

    [Fact]
    public void a_too_deep_cup_is_not_reported()
    {
        CupWithHandleDetector.Detect(CleanCup(bottom: 55m)).Should().BeNull();
    }

    [Fact]
    public void a_too_short_cup_is_not_reported()
    {
        var bars = Build(60m, new[] { (70, 100m), (10, 80m), (10, 98m), (7, 90m), (3, 96m) });

        CupWithHandleDetector.Detect(bars).Should().BeNull();
    }

    [Fact]
    public void a_handle_in_the_lower_half_of_the_cup_fails_that_rule_alone_when_the_depth_cap_is_lifted()
    {
        var bars = CleanCup(bottom: 68m, handleLow: 80m);
        var loose = new CupWithHandleOptions { MaxHandleDepthPct = 40m };

        CupWithHandleDetector.Detect(bars, loose).Should().BeNull("tay cầm nằm nửa dưới của cốc");
        CupWithHandleDetector.Detect(CleanCup(bottom: 68m, handleLow: 90m), loose).Should().NotBeNull("cùng cốc, tay cầm ở nửa trên thì đạt");
    }

    [Fact]
    public void no_handle_yet_is_not_reported()
    {
        var bars = Build(60m, new[] { (70, 100m), (25, 72m), (40, 98m), (2, 96m) });

        CupWithHandleDetector.Detect(bars).Should().BeNull();
    }

    [Fact]
    public void a_breakout_above_the_pivot_on_heavy_volume_is_broke_out_with_volume()
    {
        var c = CupWithHandleDetector.Detect(CleanCup(lastClose: 99.5m, lastVolume: 2_000_000));

        c!.State.Should().Be("broke-out-with-volume");
        c.DistanceToPivotPct.Should().BeLessThan(0m, "đã nằm trên pivot");
        c.VolumeVsAvg50.Should().BeApproximately(2.0m, 0.05m);
    }

    [Fact]
    public void a_breakout_on_ordinary_volume_is_unconfirmed()
    {
        var c = CupWithHandleDetector.Detect(CleanCup(lastClose: 99.5m, lastVolume: 1_000_000));

        c!.State.Should().Be("broke-out-unconfirmed");
    }

    [Fact]
    public void a_price_already_far_above_the_pivot_is_no_longer_a_candidate()
    {
        CupWithHandleDetector.Detect(CleanCup(lastClose: 105m)).Should().BeNull();
    }

    [Fact]
    public void a_last_bar_that_jumps_more_than_the_extension_cap_above_the_pivot_is_no_longer_a_candidate_but_just_under_the_cap_is()
    {
        // Tay cầm đi ngang dưới pivot rồi MỘT nến nhảy vọt: các nến trước vẫn dưới pivot nên tay cầm còn nguyên, chỉ riêng nến cuối vượt xa.
        List<StockPriceData> Jump(decimal lastClose) =>
            Build(60m, new[] { (70, 100m), (25, 72m), (40, 98m), (7, 90m), (2, 95m), (1, lastClose) });

        CupWithHandleDetector.Detect(Jump(103m)).Should().NotBeNull("103 chỉ cao hơn pivot 98,39 khoảng 4,7%");
        CupWithHandleDetector.Detect(Jump(103.6m)).Should().BeNull("103,6 cao hơn pivot hơn 5%");
    }

    [Fact]
    public void a_right_rim_more_than_five_percent_above_the_left_peak_is_a_different_structure_and_not_reported()
    {
        var bars = Build(60m, new[] { (70, 100m), (25, 72m), (40, 110m), (7, 100m), (3, 106m) });

        CupWithHandleDetector.Detect(bars).Should().BeNull();
    }

    [Fact]
    public void a_flat_topped_rim_starts_the_handle_at_the_last_bar_of_the_plateau()
    {
        var bars = Build(60m, new[] { (70, 100m), (25, 72m), (40, 98m), (3, 98m), (7, 90m), (3, 96m) });

        var c = CupWithHandleDetector.Detect(bars)!;

        c.HandleLengthSessions.Should().Be(10, "tay cầm tính từ nến CUỐI của đoạn vành đi ngang, không phải nến đầu");
        c.RightLipDate.Should().Be(bars[137].Date);
    }

    [Fact]
    public void a_breakout_that_happened_days_ago_leaves_no_handle_and_no_candidate()
    {
        var bars = Build(60m, new[] { (70, 100m), (25, 72m), (40, 98m), (7, 90m), (3, 99.5m), (8, 104m) });

        CupWithHandleDetector.Detect(bars).Should().BeNull();
    }

    [Fact]
    public void a_close_below_the_handle_low_cancels_the_candidate()
    {
        CupWithHandleDetector.Detect(CleanCup(lastClose: 85m)).Should().BeNull();
    }

    [Fact]
    public void not_enough_data_is_not_reported()
    {
        var bars = Build(60m, new[] { (20, 100m), (10, 80m), (5, 98m), (3, 90m), (2, 96m) });

        CupWithHandleDetector.Detect(bars).Should().BeNull();
        CupWithHandleDetector.Detect(new List<StockPriceData>()).Should().BeNull();
    }

    [Fact]
    public void the_downside_is_deterministic_stop_below_the_handle_low_and_a_measured_move_target()
    {
        var bars = CleanCup(lastClose: 96m);
        var c = CupWithHandleDetector.Detect(bars)!;
        var handleLow = bars.Skip(135).Take(9).Min(b => b.Low);          // đáy tay cầm, không tính nến cuối
        var cupDepthAbs = bars[69].High - bars[94].Low;                  // sâu tuyệt đối của cốc

        c.StopPrice.Should().BeApproximately(handleLow * 0.99m, 0.01m);
        c.RiskPct.Should().BeApproximately((c.PivotPrice - c.StopPrice) / c.PivotPrice * 100m, 0.1m);
        c.TargetPrice.Should().BeApproximately(c.PivotPrice + cupDepthAbs, 0.01m);
        c.RewardPct.Should().BeApproximately((c.TargetPrice - c.PivotPrice) / c.PivotPrice * 100m, 0.1m);
        c.RewardRisk.Should().BeApproximately((c.TargetPrice - c.PivotPrice) / (c.PivotPrice - c.StopPrice), 0.05m);
        c.RiskPct.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void it_is_deterministic_does_not_mutate_the_input_and_ignores_input_order()
    {
        var bars = CleanCup();
        var snapshot = JsonSerializer.Serialize(bars);
        var shuffled = bars.AsEnumerable().Reverse().ToList();

        var a = JsonSerializer.Serialize(CupWithHandleDetector.Detect(bars));
        var b = JsonSerializer.Serialize(CupWithHandleDetector.Detect(bars));
        var fromReversed = JsonSerializer.Serialize(CupWithHandleDetector.Detect(shuffled));

        a.Should().Be(b);
        fromReversed.Should().Be(a);
        JsonSerializer.Serialize(bars).Should().Be(snapshot);
    }

    [Fact]
    public void the_output_carries_no_score_or_probability_and_always_the_disclaimer()
    {
        var c = CupWithHandleDetector.Detect(CleanCup())!;

        // Danh sách cho phép TUYỆT ĐỐI (phản chiếu trên kiểu): thêm bất kỳ trường nào, kể cả "SuccessPct", "WinPct" hay "Confidence", đều làm test đỏ.
        typeof(PatternCandidate).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(new[]
        {
            "Type", "TypeVi", "State", "StateVi", "StartDate", "BottomDate", "RightLipDate", "CupLengthWeeks", "CupDepthPct", "HandleLengthSessions", "HandleDepthPct",
            "PivotPrice", "DistanceToPivotPct", "VolumeVsAvg50", "StopPrice", "RiskPct", "TargetPrice", "RewardPct", "RewardRisk", "Criteria", "Disclaimer"
        });
        typeof(PatternCriterion).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(new[] { "Key", "Rule", "Value", "Unit", "Passed", "Required" });
        c.Disclaimer.Should().Contain("không phải tín hiệu mua").And.Contain("khối lượng");
        c.Criteria.Should().OnlyContain(k => !string.IsNullOrWhiteSpace(k.Rule));
    }

    [Fact]
    public void thresholds_are_options_and_a_stricter_depth_cap_rejects_a_cup_the_default_accepts()
    {
        var bars = CleanCup();

        CupWithHandleDetector.Detect(bars, new CupWithHandleOptions { MaxCupDepthPct = 25m }).Should().BeNull();
        CupWithHandleDetector.Detect(bars).Should().NotBeNull();
    }
}
