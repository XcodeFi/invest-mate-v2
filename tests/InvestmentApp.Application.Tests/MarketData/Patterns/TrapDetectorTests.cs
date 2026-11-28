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

        typeof(TrapEvent).GetProperties().Select(p => p.Name.ToLowerInvariant())
            .Should().NotContain(n => n.Contains("score") || n.Contains("probab") || n.Contains("rate") || n.Contains("freq") || n.Contains("win") || n.Contains("confidence"));
        e.Disclaimer.Should().Contain("sau 5 phiên").And.Contain("không phải dự báo");
        e.TypeVi.Should().NotBeNullOrWhiteSpace();
        e.Description.Should().NotBeNullOrWhiteSpace();
    }
}
