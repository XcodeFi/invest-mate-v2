using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Patterns;

namespace InvestmentApp.Application.Tests.MarketData.Patterns;

/// <summary>Bộ dò bẫy: phá vỡ thất bại và bẫy giảm giá, xác nhận hồi tố ở t+5. Hàm thuần trên nến tổng hợp, không thống kê.</summary>
public class TrapDetectorTests
{
    private static readonly DateTime Day0 = new(2025, 1, 1);

    /// <summary>Chuỗi nến từ các giá đóng cửa liệt kê (mỗi số là một phiên); cao = đóng×1,004, thấp = đóng×0,996.</summary>
    private static List<StockPriceData> Closes(params decimal[] closes)
        => closes.Select((c, i) => new StockPriceData
        {
            Symbol = "TST", Date = Day0.AddDays(i), Open = c, High = c * 1.004m, Low = c * 0.996m, Close = c, Volume = 1_000_000, IsAdjusted = true
        }).ToList();

    private static decimal[] Flat(int n, decimal v = 100m) => Enumerable.Repeat(v, n).ToArray();

    /// <summary>Đi ngang 30 phiên quanh 100 (cao nhất 100,4, thấp nhất 99,6) rồi các giá đóng cửa tiếp theo.</summary>
    private static List<StockPriceData> After(params decimal[] next) => Closes(Flat(30).Concat(next).ToArray());

    // ---------------- phá vỡ thất bại

    [Fact]
    public void a_breakout_that_closes_back_below_the_pivot_within_five_sessions_is_a_failed_breakout_confirmed_at_t_plus_5()
    {
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100.2m);   // t = 30, thất bại ở phiên 32 (đóng 100 < pivot 100,4 nhưng vẫn trên hỗ trợ 99,6), đủ cửa sổ tới 35

        var events = TrapDetector.Detect(bars);

        var e = events.Should().ContainSingle().Subject;
        e.Type.Should().Be("failed-breakout");
        e.BreakoutDate.Should().Be(bars[30].Date);
        e.ReturnDate.Should().Be(bars[32].Date);
        e.ConfirmedDate.Should().Be(bars[35].Date);
        e.LevelPrice.Should().BeApproximately(100.4m, 0.001m);
    }

    [Fact]
    public void the_failed_breakout_carries_its_downside_distance_and_the_measured_excursion()
    {
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100.2m);

        var e = TrapDetector.Detect(bars).Single();

        // cắt lỗ = đáy thấp nhất 10 phiên trước nến phá vỡ × 0,99 = 99,6 × 0,99
        e.StopPrice.Should().BeApproximately(99.6m * 0.99m, 0.001m);
        e.RiskPct.Should().BeApproximately((100.4m - 99.6m * 0.99m) / 100.4m * 100m, 0.1m);
        // đo được: nến thấp nhất trong cửa sổ t+1..t+5 là 100 × 0,996 = 99,6
        e.ExcursionPct.Should().BeApproximately((100.4m - 100m * 0.996m) / 100.4m * 100m, 0.1m);
    }

    [Fact]
    public void a_window_that_ends_exactly_on_the_last_bar_is_complete_and_the_event_is_reported()
    {
        var bars = After(103m, 100m, 100m, 100m, 100m, 100m);   // t = 30, t+5 = 35 = nến cuối

        TrapDetector.Detect(bars).Should().ContainSingle().Which.ConfirmedDate.Should().Be(bars[35].Date);
    }

    [Fact]
    public void a_close_exactly_at_the_pivot_is_not_a_breakout()
    {
        // pivot = 100,4 (cao nhất 20 phiên); đóng cửa đúng 100,4 chưa phải vượt
        var bars = After(100.4m, 100m, 100m, 100m, 100m, 100m, 100m);

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void a_close_exactly_at_the_pivot_after_the_breakout_is_not_back_below_it()
    {
        var bars = After(103m, 100.4m, 100.4m, 100.4m, 100.4m, 100.4m, 100.4m);   // đóng cửa đúng pivot: chưa "dưới"

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void only_the_most_recent_events_are_returned_up_to_the_limit()
    {
        var next = new List<decimal> { 103m, 100m, 100m, 100m, 100m, 100m };
        next.AddRange(Flat(25));
        next.AddRange(new decimal[] { 104m, 100m, 100m, 100m, 100m, 100m, 100m });
        var bars = After(next.ToArray());

        var one = TrapDetector.Detect(bars, new TrapOptions { MaxEvents = 1 });

        one.Should().ContainSingle().Which.BreakoutDate.Should().Be(bars[61].Date);   // lần thứ hai (t = 61)
    }

    [Fact]
    public void a_breakout_that_holds_above_the_pivot_for_five_sessions_is_not_a_trap()
    {
        var bars = After(103m, 104m, 103m, 105m, 104m, 103m, 104m);

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void falling_back_only_after_the_window_of_five_sessions_does_not_count()
    {
        var bars = After(103m, 104m, 103m, 105m, 104m, 103m, 100m, 100m);   // phiên t+6 mới rơi xuống

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void a_breakout_whose_window_is_not_complete_yet_is_not_reported_even_if_it_already_failed()
    {
        var bars = After(103m, 100m, 100m);   // t = 30, mới có 2 phiên sau: chưa tới t+5

        TrapDetector.Detect(bars).Should().BeEmpty("chưa xác nhận: không dự đoán");
    }

    [Fact]
    public void only_the_first_bar_of_a_breakout_counts_not_every_bar_above_the_pivot()
    {
        var bars = After(103m, 104m, 100m, 100m, 100m, 100m, 100m);   // hai nến liên tiếp trên pivot, rồi thất bại

        TrapDetector.Detect(bars).Should().ContainSingle();
    }

    // ---------------- bẫy giảm giá

    [Fact]
    public void a_break_below_support_that_closes_back_above_within_five_sessions_is_a_bear_trap_with_its_downside_distance()
    {
        var bars = After(96m, 97m, 101m, 101m, 101m, 101m, 101m);   // t = 30 thủng hỗ trợ 99,6; phiên 32 đóng lại trên

        var events = TrapDetector.Detect(bars);

        var e = events.Should().ContainSingle().Subject;
        e.Type.Should().Be("bear-trap");
        e.BreakoutDate.Should().Be(bars[30].Date);
        e.ReturnDate.Should().Be(bars[32].Date);
        e.ConfirmedDate.Should().Be(bars[35].Date);
        e.LevelPrice.Should().BeApproximately(99.6m, 0.001m);
        e.StopPrice.Should().BeApproximately(96m * 0.996m * 0.99m, 0.001m);   // đáy của đợt thủng giả × 0,99
        e.RiskPct.Should().BeApproximately((99.6m - 96m * 0.996m * 0.99m) / 99.6m * 100m, 0.1m);
        e.ExcursionPct.Should().BeApproximately((99.6m - 96m * 0.996m) / 99.6m * 100m, 0.1m);
    }

    [Fact]
    public void a_close_exactly_at_the_support_is_not_back_above_it()
    {
        var bars = After(96m, 99.6m, 99.6m, 99.6m, 99.6m, 99.6m, 99.6m);   // hỗ trợ = 99,6; đóng cửa đúng 99,6 chưa phải "trên"

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void the_bear_trap_stop_uses_only_the_false_break_up_to_the_return_not_a_later_dip_after_it()
    {
        // t = 30 đóng 96 (thủng), phiên 31 đóng 101 (quay lại), phiên 32 lại rơi xuống 94: đáy này nằm SAU lúc quay lại nên không tính vào cắt lỗ của mốc đầu
        var bars = After(96m, 101m, 94m, 101m, 101m, 101m, 101m);

        var e = TrapDetector.Detect(bars).Should().ContainSingle(x => x.Type == "bear-trap").Subject;

        e.StopPrice.Should().BeApproximately(96m * 0.996m * 0.99m, 0.001m);
    }

    [Fact]
    public void a_break_below_support_that_never_recovers_in_five_sessions_is_not_a_trap()
    {
        var bars = After(96m, 95m, 96m, 95m, 94m, 95m, 96m);

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    // ---------------- chung

    [Fact]
    public void a_second_failed_breakout_within_the_cooldown_is_not_reported_again()
    {
        // Hai lần phá vỡ thất bại cách nhau 12 phiên (< 20): chỉ lần đầu được báo.
        var next = new List<decimal> { 103m, 100m, 100m, 100m, 100m, 100m };
        next.AddRange(Flat(6));
        next.AddRange(new decimal[] { 104m, 100m, 100m, 100m, 100m, 100m, 100m });
        var bars = After(next.ToArray());

        TrapDetector.Detect(bars).Should().ContainSingle();
    }

    [Fact]
    public void the_same_pattern_after_the_cooldown_is_reported_again()
    {
        var next = new List<decimal> { 103m, 100m, 100m, 100m, 100m, 100m };
        next.AddRange(Flat(25));
        next.AddRange(new decimal[] { 104m, 100m, 100m, 100m, 100m, 100m, 100m });
        var bars = After(next.ToArray());

        TrapDetector.Detect(bars).Should().HaveCount(2);
    }

    [Fact]
    public void not_enough_data_or_an_empty_series_gives_no_events()
    {
        TrapDetector.Detect(Closes(100m, 103m, 99m)).Should().BeEmpty();
        TrapDetector.Detect(new List<StockPriceData>()).Should().BeEmpty();
    }

    [Fact]
    public void events_are_ordered_oldest_first_deterministic_and_do_not_depend_on_input_order_or_mutate_it()
    {
        var next = new List<decimal> { 103m, 100m, 100m, 100m, 100m, 100m };
        next.AddRange(Flat(25));
        next.AddRange(new decimal[] { 96m, 97m, 101m, 101m, 101m, 101m, 101m });
        var bars = After(next.ToArray());
        var snapshot = JsonSerializer.Serialize(bars);

        var a = TrapDetector.Detect(bars);
        var reversed = TrapDetector.Detect(bars.AsEnumerable().Reverse().ToList());

        a.Select(e => e.Type).Should().Equal("failed-breakout", "bear-trap");
        a.Select(e => e.BreakoutDate).Should().BeInAscendingOrder();
        JsonSerializer.Serialize(reversed).Should().Be(JsonSerializer.Serialize(a));
        JsonSerializer.Serialize(bars).Should().Be(snapshot);
    }

    [Fact]
    public void the_confirmation_window_is_an_option()
    {
        var bars = After(103m, 104m, 100m, 100m, 100m, 100m);   // thất bại ở t+2

        TrapDetector.Detect(bars, new TrapOptions { ConfirmSessions = 3 }).Should().ContainSingle();
        TrapDetector.Detect(bars, new TrapOptions { ConfirmSessions = 1 }).Should().BeEmpty("cửa sổ 1 phiên: t+1 vẫn trên pivot");
    }

    [Fact]
    public void the_output_has_no_probability_frequency_or_score_members_and_always_the_disclaimer()
    {
        var e = TrapDetector.Detect(After(103m, 102m, 100m, 100m, 100m, 100m, 100.2m)).Single();

        // Danh sách cho phép TUYỆT ĐỐI các thành viên của TrapEvent (phản chiếu trên kiểu, không trên một mẫu): thêm bất kỳ trường nào, kể cả "SuccessPct", "WinPct"
        // hay "Confidence", đều làm test đỏ và buộc người thêm phải sửa test này một cách có chủ ý. Không có trường thống kê nào được phép lọt qua.
        typeof(TrapEvent).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(new[]
        {
            "Type", "TypeVi", "BreakoutDate", "ReturnDate", "ConfirmedDate", "LevelPrice", "StopPrice", "RiskPct", "ExcursionPct", "Description", "Disclaimer"
        });
        e.Disclaimer.Should().Contain("sau 5 phiên").And.Contain("không phải dự báo");
        e.TypeVi.Should().NotBeNullOrWhiteSpace();
        e.Description.Should().NotBeNullOrWhiteSpace();
    }

    // ---------------- tiền "nếu sai": số đúng từng chữ số

    [Fact]
    public void the_failed_breakout_risk_pct_is_measured_from_the_pivot_exactly()
    {
        // một nến trong 10 phiên trước có bấc thấp 90 (đóng cửa vẫn 100): cắt lỗ = 90 × 0,99 = 89,1; rủi ro = (100,4 − 89,1) / 100,4 = 11,3%
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);
        bars[25].Low = 90m;

        var e = TrapDetector.Detect(bars).Single();

        e.StopPrice.Should().Be(89.1m);
        e.RiskPct.Should().Be(11.3m, "chia cho cắt lỗ thay vì pivot sẽ ra 12,7");
    }

    [Fact]
    public void the_bear_trap_risk_and_excursion_are_measured_from_the_support_exactly()
    {
        var bars = After(96m, 97m, 100m, 100m, 100m, 100m, 100m);   // thủng 96 (thấp 95,616), quay lại ở phiên 32

        var e = TrapDetector.Detect(bars).Single(x => x.Type == "bear-trap");

        e.StopPrice.Should().Be(95.616m * 0.99m);
        e.RiskPct.Should().Be(5.0m, "(99,6 − 94,65984) / 99,6 = 4,96%; chia cho cắt lỗ sẽ ra 5,2");
        e.ExcursionPct.Should().Be(4.0m);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void the_stop_uses_the_lowest_low_of_the_ten_sessions_before_the_breakout_and_not_older_ones(int sessionsBefore, bool counts)
    {
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);
        bars[30 - sessionsBefore].Low = 90m;

        var e = TrapDetector.Detect(bars).Single();

        e.StopPrice.Should().Be(counts ? 89.1m : 99.6m * 0.99m);
        e.RiskPct.Should().Be(counts ? 11.3m : 1.8m);
    }

    [Fact]
    public void the_excursion_looks_only_at_the_five_sessions_after_the_breakout_not_at_the_breakout_bar_itself()
    {
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);
        bars[30].Low = 80m;   // nến phá vỡ có bấc rất thấp: không được tính vào mức đi xuyên qua

        var e = TrapDetector.Detect(bars).Single();

        e.ExcursionPct.Should().Be(0.8m, "(100,4 − 99,6) / 100,4; tính cả nến phá vỡ sẽ ra 20,3");
        e.StopPrice.Should().Be(99.6m * 0.99m, "nến phá vỡ cũng không nằm trong cửa sổ cắt lỗ (các phiên TRƯỚC nó)");
    }

    // ---------------- biên cửa sổ

    [Fact]
    public void a_high_exactly_twenty_sessions_before_the_breakout_is_part_of_the_pivot_but_twenty_one_is_not()
    {
        var inside = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);
        inside[10].High = 105m;     // t − 20: cao hơn mọi nến khác → pivot = 105 → đóng 103 không phải vượt
        var outside = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);
        outside[9].High = 105m;     // t − 21: ngoài cửa sổ → pivot vẫn 100,4

        TrapDetector.Detect(inside).Should().BeEmpty();
        TrapDetector.Detect(outside).Should().ContainSingle();
    }

    [Fact]
    public void a_low_exactly_twenty_sessions_before_the_break_is_part_of_the_support_but_twenty_one_is_not()
    {
        var inside = After(96m, 97m, 100m, 100m, 100m, 100m, 100m);
        inside[10].Low = 90m;       // t − 20: hỗ trợ = 90 → đóng 96 không phải thủng
        var outside = After(96m, 97m, 100m, 100m, 100m, 100m, 100m);
        outside[9].Low = 90m;       // t − 21: ngoài cửa sổ

        TrapDetector.Detect(inside).Should().BeEmpty();
        TrapDetector.Detect(outside).Should().ContainSingle(x => x.Type == "bear-trap");
    }

    [Fact]
    public void the_earliest_detectable_breakout_is_the_twenty_second_bar_and_the_twenty_first_is_not_examined()
    {
        var first = Closes(Flat(21).Concat(new[] { 103m, 100m, 100m, 100m, 100m, 100m }).ToArray());      // t = 21
        var before = Closes(Flat(20).Concat(new[] { 103m, 100m, 100m, 100m, 100m, 100m }).ToArray());     // t = 20

        TrapDetector.Detect(first).Should().ContainSingle();
        TrapDetector.Detect(before).Should().BeEmpty("chưa đủ lịch sử để xét nến thứ 21");
    }

    [Theory]
    [InlineData(20, 2)]
    [InlineData(19, 1)]
    public void a_second_failed_breakout_is_reported_only_when_it_is_at_least_twenty_sessions_after_the_first(int gap, int expected)
    {
        var closes = Flat(30).Concat(new[] { 103m, 100m, 100m, 100m, 100m, 100m }).ToList();   // t1 = 30
        closes.AddRange(Flat(gap - 6));
        closes.AddRange(new[] { 104m, 100m, 100m, 100m, 100m, 100m, 100m });                   // t2 = 30 + gap

        TrapDetector.Detect(Closes(closes.ToArray())).Where(e => e.Type == "failed-breakout").Should().HaveCount(expected);
    }

    [Theory]
    [InlineData(20, 2)]
    [InlineData(19, 1)]
    public void a_second_bear_trap_is_reported_only_when_it_is_at_least_twenty_sessions_after_the_first(int gap, int expected)
    {
        var closes = Flat(30).Concat(new[] { 90m, 100m, 100m, 100m, 100m, 100m }).ToList();   // t1 = 30
        closes.AddRange(Flat(gap - 6));
        closes.AddRange(new[] { 85m, 100m, 100m, 100m, 100m, 100m, 100m });                    // t2 = 30 + gap, thủng dưới đáy 89,64

        TrapDetector.Detect(Closes(closes.ToArray())).Where(e => e.Type == "bear-trap").Should().HaveCount(expected);
    }

    [Fact]
    public void a_close_exactly_at_the_support_is_not_a_break_below_it()
    {
        var bars = After(99.6m, 100m, 100m, 100m, 100m, 100m, 100m);   // hỗ trợ = 99,6; đóng cửa đúng 99,6 chưa phải thủng

        TrapDetector.Detect(bars).Should().BeEmpty();
    }

    [Fact]
    public void a_return_exactly_on_the_fifth_session_still_counts_for_a_failed_breakout()
    {
        var bars = After(103m, 104m, 104m, 104m, 104m, 100m);   // t = 30; t+1..t+4 trên pivot, t+5 (phiên 35) đóng 100 < 100,4

        var e = TrapDetector.Detect(bars).Should().ContainSingle().Subject;

        e.ReturnDate.Should().Be(bars[35].Date);
        e.ConfirmedDate.Should().Be(bars[35].Date);
    }

    [Fact]
    public void a_return_exactly_on_the_fifth_session_still_counts_for_a_bear_trap()
    {
        var bars = After(96m, 96m, 96m, 96m, 96m, 100m);   // thủng ở t = 30, quay lại trên hỗ trợ 99,6 đúng ở t+5

        var e = TrapDetector.Detect(bars).Should().ContainSingle(x => x.Type == "bear-trap").Subject;

        e.ReturnDate.Should().Be(bars[35].Date);
    }

    [Fact]
    public void the_excursion_covers_the_whole_confirmation_window_including_lows_after_the_first_return()
    {
        var bars = After(103m, 102m, 100m, 100m, 100m, 100m, 100m);   // quay lại ở phiên 32
        bars[34].Low = 90m;                                          // bấc thấp ở t+4, SAU lúc quay lại nhưng còn trong cửa sổ

        var e = TrapDetector.Detect(bars).Single();

        e.ExcursionPct.Should().Be(10.4m, "(100,4 − 90) / 100,4");
    }

    [Fact]
    public void each_type_carries_its_own_vietnamese_name()
    {
        var failed = TrapDetector.Detect(After(103m, 102m, 100m, 100m, 100m, 100m, 100m)).Single();
        var bear = TrapDetector.Detect(After(96m, 97m, 100m, 100m, 100m, 100m, 100m)).Single(x => x.Type == "bear-trap");

        failed.TypeVi.Should().Be("Phá vỡ thất bại");
        bear.TypeVi.Should().Be("Bẫy giảm giá");
        failed.Description.Should().Contain("trở lại dưới pivot");
        bear.Description.Should().Contain("trở lại trên hỗ trợ");
    }
}
