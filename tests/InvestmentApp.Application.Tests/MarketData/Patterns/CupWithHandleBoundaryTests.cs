using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Patterns;

namespace InvestmentApp.Application.Tests.MarketData.Patterns;

/// <summary>
/// Từng ngưỡng của bộ dò được ghim ở ĐÚNG giá trị biên với tuỳ chọn mặc định. Nến phẳng (mở = cao = thấp = đóng) nên các số dựng ra khớp từng chữ số:
/// đỉnh trái, đáy, vành phải và đáy tay cầm đều là số đã cho, không có độ lệch 0,4%.
/// </summary>
public class CupWithHandleBoundaryTests
{
    private static readonly DateTime Day0 = new(2025, 1, 1);

    /// <summary>Chuỗi: đà tăng <paramref name="warm"/> phiên tới đỉnh trái, <paramref name="down"/> phiên xuống đáy, <paramref name="up"/> phiên lên vành phải,
    /// rồi tay cầm dài <paramref name="handle"/> phiên (đáy tay cầm đúng <paramref name="handleLow"/>, nến cuối đóng <paramref name="last"/>).</summary>
    private static List<StockPriceData> Exact(decimal left = 100m, decimal bottom = 70m, decimal rim = 98m, decimal handleLow = 88m, decimal last = 94m,
        int warm = 30, int down = 15, int up = 20, int handle = 12, long lastVolume = 1_000_000)
    {
        var closes = new List<decimal>();
        for (var k = 1; k <= warm; k++) closes.Add(60m + (left - 60m) * k / warm);
        for (var k = 1; k <= down; k++) closes.Add(left + (bottom - left) * k / down);
        for (var k = 1; k <= up; k++) closes.Add(bottom + (rim - bottom) * k / up);
        var body = handle - 1;                       // các nến tay cầm trước nến cuối
        var mid = (body + 1) / 2;                    // nến có giá thấp nhất
        for (var i = 1; i <= body; i++)
            closes.Add(i == mid ? handleLow : handleLow + (rim - handleLow) * 0.5m * Math.Abs(i - mid) / Math.Max(mid, 1));
        closes.Add(last);
        return closes.Select((c, i) => new StockPriceData
        {
            Symbol = "TST", Date = Day0.AddDays(i), Open = c, High = c, Low = c, Close = c, Volume = i == closes.Count - 1 ? lastVolume : 1_000_000, IsAdjusted = true
        }).ToList();
    }

    private static PatternCandidate? Run(List<StockPriceData> bars) => CupWithHandleDetector.Detect(bars);

    [Fact]
    public void the_baseline_exact_cup_is_found_so_every_boundary_case_below_changes_exactly_one_thing()
    {
        var c = Run(Exact());

        c.Should().NotBeNull();
        c!.CupLengthWeeks.Should().Be(7m);
        c.CupDepthPct.Should().Be(30m);
        c.HandleLengthSessions.Should().Be(12);
        c.PivotPrice.Should().Be(98m);
        c.StopPrice.Should().Be(88m * 0.99m);
    }

    public static IEnumerable<object[]> Cases()
    {
        // tên, chuỗi, kỳ vọng "tìm thấy"
        yield return new object[] { "MinBars: đúng 60 phiên", Exact(warm: 12, handle: 13), true };
        yield return new object[] { "MinBars: 59 phiên", Exact(warm: 11, handle: 13), false };
        yield return new object[] { "cốc đúng 7,0 tuần (35 phiên)", Exact(up: 20), true };
        yield return new object[] { "cốc 6,8 tuần (34 phiên)", Exact(up: 19), false };
        yield return new object[] { "cốc dài đúng 65 tuần (325 phiên)", Exact(warm: 30, down: 150, up: 175), true };
        yield return new object[] { "cốc dài 65,2 tuần (326 phiên)", Exact(warm: 30, down: 150, up: 176), false };
        yield return new object[] { "cốc sâu đúng 33,0%", Exact(bottom: 67m, handleLow: 85m, last: 94m), true };
        yield return new object[] { "cốc sâu 33,1%", Exact(bottom: 66.9m, handleLow: 85m, last: 94m), false };
        yield return new object[] { "tay cầm sâu đúng 8,0%", Exact(handleLow: 90.16m, last: 94m), true };
        yield return new object[] { "tay cầm sâu 7,99%", Exact(handleLow: 90.17m, last: 94m), false };
        yield return new object[] { "tay cầm sâu đúng 15,0%", Exact(bottom: 67m, handleLow: 83.3m, last: 94m), true };
        yield return new object[] { "tay cầm sâu 15,1%", Exact(bottom: 67m, handleLow: 83.2m, last: 94m), false };
        yield return new object[] { "vành phải hồi đúng 90%", Exact(rim: 90m, handleLow: 81m, last: 87m), true };
        yield return new object[] { "vành phải hồi 89,9%", Exact(rim: 89.9m, handleLow: 81m, last: 87m), false };
        yield return new object[] { "tay cầm đúng 5 phiên", Exact(handle: 5), true };
        yield return new object[] { "tay cầm 4 phiên", Exact(handle: 4), false };
        yield return new object[] { "tay cầm đúng 25 phiên", Exact(handle: 25), true };
        yield return new object[] { "tay cầm 26 phiên", Exact(handle: 26), false };
        yield return new object[] { "đáy tay cầm đúng giữa cốc", Exact(bottom: 70m, rim: 98m, handleLow: 84m, last: 94m), true };
        yield return new object[] { "đáy tay cầm dưới giữa cốc 0,01", Exact(bottom: 70m, rim: 98m, handleLow: 83.99m, last: 94m), false };
        yield return new object[] { "giá đóng cửa đúng bằng đáy tay cầm", Exact(handleLow: 88m, last: 88m), true };
        yield return new object[] { "giá đóng cửa thấp hơn đáy tay cầm 0,01", Exact(handleLow: 88m, last: 87.99m), false };
        yield return new object[] { "vượt pivot đúng 5,0%", Exact(last: 102.9m), true };
        yield return new object[] { "vượt pivot 5,01%", Exact(last: 102.91m), false };
    }

    [Fact]
    public void each_threshold_is_pinned_at_its_exact_boundary()
    {
        var wrong = new List<string>();
        foreach (var o in Cases())
        {
            var name = (string)o[0];
            var found = Run((List<StockPriceData>)o[1]) != null;
            if (found != (bool)o[2]) wrong.Add($"{name}: kỳ vọng {(bool)o[2]}, thực tế {found}");
        }

        wrong.Should().BeEmpty();
    }

    [Fact]
    public void the_cup_depth_floor_of_12_percent_is_exact_when_the_handle_depth_floor_is_lifted_out_of_the_way()
    {
        // Với mặc định, cốc nông 12% không thể có tay cầm sâu ≥ 8% mà vẫn ở nửa trên của cốc; nên cô lập bằng cách hạ sàn độ sâu tay cầm.
        var open = new CupWithHandleOptions { MinHandleDepthPct = 0m };

        CupWithHandleDetector.Detect(Exact(bottom: 88m, rim: 98m, handleLow: 94m, last: 96m), open).Should().NotBeNull("đúng 12,0%");
        CupWithHandleDetector.Detect(Exact(bottom: 88.1m, rim: 98m, handleLow: 94m, last: 96m), open).Should().BeNull("11,9%");
    }

    [Fact]
    public void state_boundaries_close_equal_to_the_pivot_near_pivot_at_exactly_3_percent_and_volume_at_exactly_1_4_times()
    {
        Run(Exact(last: 98m))!.State.Should().Be("near-pivot", "đóng cửa đúng pivot chưa phải vượt");
        Run(Exact(last: 98.01m))!.State.Should().Be("broke-out-unconfirmed");

        Run(Exact(last: 95.06m))!.State.Should().Be("near-pivot", "cách pivot đúng 3,0%");
        Run(Exact(last: 95.05m))!.State.Should().Be("forming", "cách pivot 3,01%");

        Run(Exact(last: 99m, lastVolume: 1_400_000))!.State.Should().Be("broke-out-with-volume", "đúng 1,4×");
        Run(Exact(last: 99m, lastVolume: 1_399_999))!.State.Should().Be("broke-out-unconfirmed");
    }

    [Fact]
    public void the_volume_ratio_is_the_last_bar_over_the_mean_of_the_previous_50_bars_not_including_itself()
    {
        var bars = Exact(last: 99m, lastVolume: 2_000_000);
        // làm lệch trung bình: phiên thứ 3 tính từ cuối có khối lượng 3 triệu → trung bình 50 phiên trước = (49 × 1 + 3) / 50 = 1,04 triệu
        bars[^3].Volume = 3_000_000;

        var c = Run(bars)!;

        c.VolumeVsAvg50.Should().Be(1.92m, "2,0 / 1,04 = 1,923; nếu tính cả nến cuối vào trung bình sẽ ra số khác");
    }

    [Fact]
    public void the_cup_bottom_is_searched_before_the_handle_sessions_so_a_wick_in_the_last_bars_does_not_become_the_bottom()
    {
        var bars = Exact(last: 94m);
        bars[^1].Low = 40m;   // chỉ cái bấc của nến cuối đâm xuống dưới đáy cốc

        var c = Run(bars);

        c.Should().NotBeNull("đáy cốc vẫn là đáy thật 70, không phải cái bấc của nến cuối");
        c!.CupDepthPct.Should().Be(30m);
    }

    [Fact]
    public void a_single_last_bar_that_closes_below_the_handle_low_cancels_the_candidate_while_the_handle_low_still_ignores_that_bar()
    {
        // đáy tay cầm 88 ở giữa tay cầm; nến cuối đóng 87 (< 88). Nếu đáy tay cầm tính cả nến cuối (87) thì điều kiện "chưa thủng" không bao giờ sai.
        Run(Exact(handleLow: 88m, last: 87m)).Should().BeNull();
        Run(Exact(handleLow: 88m, last: 88m)).Should().NotBeNull();
    }
}
