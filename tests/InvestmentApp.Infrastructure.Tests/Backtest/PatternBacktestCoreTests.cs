using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Tests.Backtest;

namespace InvestmentApp.Infrastructure.Tests.Backtest;

/// <summary>Luật mô phỏng và thống kê của lõi back-test, trên nến tổng hợp (không cần cơ sở dữ liệu).</summary>
public class PatternBacktestCoreTests
{
    private static readonly DateTime Day0 = new(2025, 1, 1);

    private static StockPriceData Bar(int i, decimal open, decimal high, decimal low, decimal close) => new()
    {
        Symbol = "TST", Date = Day0.AddDays(i), Open = open, High = high, Low = low, Close = close, Volume = 1_000_000, IsAdjusted = true
    };

    private static BacktestSignal Sig(int index, decimal level = 100m, decimal stop = 95m, EventKind kind = EventKind.Donchian20)
        => new("TST", kind, index, Day0.AddDays(index), level, stop);

    /// <summary>Nến tín hiệu đóng 101 (chỉ số 0), rồi các nến cho trước.</summary>
    private static List<StockPriceData> Series(params (decimal o, decimal h, decimal l, decimal c)[] next)
    {
        var bars = new List<StockPriceData> { Bar(0, 100, 101, 99, 101) };
        bars.AddRange(next.Select((x, i) => Bar(i + 1, x.o, x.h, x.l, x.c)));
        return bars;
    }

    // ---------------- mô phỏng một lệnh

    [Fact]
    public void reaching_level_plus_two_r_before_the_stop_is_a_success_and_entry_is_the_next_open()
    {
        // mức 100, cắt lỗ 95, R = 5, mục tiêu 110
        var bars = Series((101m, 106m, 100m, 105m), (105m, 111m, 104m, 110m));

        var t = PatternBacktestCore.Simulate(Sig(0), bars);

        t.Outcome.Should().Be(TradeOutcome.Success);
        t.Entry.Should().Be(101m);
        t.R.Should().Be(5m);
        t.Sessions.Should().Be(2);
    }

    [Fact]
    public void touching_the_stop_first_is_a_trap()
    {
        var bars = Series((101m, 103m, 100m, 102m), (102m, 103m, 94m, 95m), (95m, 120m, 94m, 118m));

        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(TradeOutcome.Trap);
    }

    [Fact]
    public void a_bar_that_touches_both_the_stop_and_the_target_counts_as_the_stop()
    {
        var bars = Series((101m, 112m, 94m, 100m));

        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(TradeOutcome.Trap);
    }

    [Fact]
    public void a_gap_open_below_the_stop_is_a_trap_even_without_a_low_touch_logic_difference()
    {
        var bars = Series((93m, 96m, 92m, 95m));   // mở cửa 93 < cắt lỗ 95

        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(TradeOutcome.Trap);
    }

    [Fact]
    public void a_gap_open_above_the_target_is_a_success()
    {
        var bars = Series((111m, 113m, 110m, 112m));   // mở 111 ≥ 110, nhưng không vượt kịch trần 101 × 1,07 = 108,07? 111 > 108,07 nên bị bỏ qua
        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(TradeOutcome.Skipped, "mở cửa ≥ +7% so với đóng cửa tín hiệu là kịch trần, không khớp được");

        var ok = Series((106m, 111m, 105m, 110m));
        PatternBacktestCore.Simulate(Sig(0), ok).Outcome.Should().Be(TradeOutcome.Success);
    }

    [Fact]
    public void twenty_quiet_sessions_end_in_a_timeout_and_the_twenty_first_is_not_looked_at()
    {
        var quiet = Enumerable.Repeat((101m, 103m, 98m, 101m), 20).ToArray();
        var bars = Series(quiet);
        bars.Add(Bar(21, 101, 150, 100, 150));   // phiên 21: đã quá hạn, không được tính là thành công

        var t = PatternBacktestCore.Simulate(Sig(0), bars);

        t.Outcome.Should().Be(TradeOutcome.Timeout);
        t.Sessions.Should().Be(20);
    }

    [Fact]
    public void the_limit_up_price_is_rounded_down_to_the_hose_tick_so_a_locked_open_is_not_filled()
    {
        // đóng cửa tín hiệu 101 (nghìn đồng) → 101.000 × 1,07 = 108.070 → bước giá 100 → kịch trần thật = 108.000 = 108,0
        PatternBacktestCore.LimitUpPrice(101m).Should().Be(108.0m);
        PatternBacktestCore.Simulate(Sig(0), Series((108.0m, 109m, 107m, 108m))).Outcome.Should().Be(TradeOutcome.Skipped, "mở cửa khoá kịch trần (đã làm tròn xuống bước giá) không khớp được");
        PatternBacktestCore.Simulate(Sig(0), Series((107.9m, 109m, 107m, 108m))).Outcome.Should().NotBe(TradeOutcome.Skipped);
    }

    [Theory]
    [InlineData(9.0, 9.63)]       // dưới 10.000đ: bước 10đ; 9.000 × 1,07 = 9.630
    [InlineData(9.5, 10.15)]      // 9.500 × 1,07 = 10.165 → vùng 10.000–49.950: bước 50đ → 10.150
    [InlineData(40.0, 42.8)]      // 42.800 chia hết 50
    [InlineData(50.5, 54.0)]      // 54.035 → bước 100 → 54.000
    [InlineData(101000, 108000)]  // đơn vị đồng: cùng quy tắc
    public void limit_up_rounding_follows_the_three_hose_tick_bands_in_either_unit(double close, double expected)
    {
        PatternBacktestCore.LimitUpPrice((decimal)close).Should().Be((decimal)expected);
    }

    [Fact]
    public void an_open_at_or_above_the_target_is_skipped_because_no_reward_is_left_but_just_below_is_filled()
    {
        // mức 100, cắt lỗ 98 → R = 2, mục tiêu 104; đóng cửa tín hiệu 101 nên kịch trần 108,0 không chặn
        var sig = Sig(0, level: 100m, stop: 98m);

        PatternBacktestCore.Simulate(sig, Series((105m, 106m, 104.5m, 105m))).Outcome.Should().Be(TradeOutcome.Skipped, "mở cửa 105 đã trên mục tiêu 104");
        PatternBacktestCore.Simulate(sig, Series((104m, 105m, 103.5m, 104.5m))).Outcome.Should().Be(TradeOutcome.Skipped, "mở cửa đúng bằng mục tiêu");
        PatternBacktestCore.Simulate(sig, Series((103.9m, 104.5m, 103m, 104m))).Outcome.Should().Be(TradeOutcome.Success, "mở cửa dưới mục tiêu thì khớp và chạm mục tiêu");
    }

    [Fact]
    public void a_trade_with_fewer_than_twenty_sessions_left_and_no_exit_is_not_observed_not_a_timeout()
    {
        var bars = Series((101m, 103m, 98m, 101m), (101m, 103m, 98m, 101m), (101m, 103m, 98m, 101m), (101m, 103m, 98m, 101m), (101m, 103m, 98m, 101m));   // chỉ 5 phiên sau

        var t = PatternBacktestCore.Simulate(Sig(0), bars);

        t.Outcome.Should().Be(TradeOutcome.NotObserved);
        PatternBacktestCore.Summarise(EventKind.Donchian20, new[] { t }).Should().Match<KindSummary>(x => x.Episodes == 0 && x.NotObserved == 1 && x.Timeout == 0);
    }

    [Theory]
    [InlineData(20, TradeOutcome.Timeout)]
    [InlineData(19, TradeOutcome.NotObserved)]
    public void exactly_twenty_observed_sessions_is_a_timeout_and_nineteen_is_still_not_observed(int quietBars, TradeOutcome expected)
    {
        var bars = Series(Enumerable.Repeat((101m, 103m, 98m, 101m), quietBars).ToArray());

        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(expected);
    }

    [Fact]
    public void a_trade_that_resolves_before_the_data_ends_is_counted_even_with_few_sessions_left()
    {
        var success = Series((101m, 111m, 100m, 110m));
        var trap = Series((101m, 103m, 94m, 95m));

        PatternBacktestCore.Simulate(Sig(0), success).Outcome.Should().Be(TradeOutcome.Success);
        PatternBacktestCore.Simulate(Sig(0), trap).Outcome.Should().Be(TradeOutcome.Trap);
    }

    [Fact]
    public void a_low_exactly_at_the_stop_is_a_trap()
    {
        var bars = Series((101m, 103m, 95m, 100m));   // thấp đúng bằng cắt lỗ 95

        PatternBacktestCore.Simulate(Sig(0), bars).Outcome.Should().Be(TradeOutcome.Trap);
    }

    [Fact]
    public void the_failure_window_is_exactly_five_sessions()
    {
        var onFifth = Series((101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 101m, 97m, 99m));
        var onSixth = Series((101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 102m, 100m, 101m), (101m, 101m, 97m, 99m));

        PatternBacktestCore.Simulate(Sig(0), onFifth).FailedWithin5.Should().BeTrue("đóng dưới mức ở phiên thứ 5");
        PatternBacktestCore.Simulate(Sig(0), onSixth).FailedWithin5.Should().BeFalse("đóng dưới mức ở phiên thứ 6");
    }

    [Fact]
    public void no_next_bar_or_a_non_positive_r_is_skipped()
    {
        PatternBacktestCore.Simulate(Sig(0), Series()).Outcome.Should().Be(TradeOutcome.Skipped);
        PatternBacktestCore.Simulate(Sig(0, level: 95m, stop: 95m), Series((101m, 103m, 100m, 102m))).Outcome.Should().Be(TradeOutcome.Skipped);
    }

    [Fact]
    public void the_two_session_adverse_excursion_only_covers_the_first_two_bars_while_the_full_one_runs_to_the_exit()
    {
        // vào 101; thấp nhất hai phiên đầu = 98 (3,0%); phiên 3 thấp 96 (4,95%) rồi chạm cắt lỗ ở phiên 4
        var bars = Series((101m, 102m, 99m, 100m), (100m, 101m, 98m, 99m), (99m, 100m, 96m, 97m), (97m, 98m, 94m, 95m));

        var t = PatternBacktestCore.Simulate(Sig(0), bars);

        t.Outcome.Should().Be(TradeOutcome.Trap);
        t.Adverse2Pct.Should().BeApproximately(3m / 101m, 0.0001m);
        t.AdverseFullPct.Should().BeApproximately(7m / 101m, 0.0001m, "tới phiên chạm cắt lỗ: đáy thấp nhất 94");
    }

    [Fact]
    public void a_breakout_that_closes_back_below_the_level_within_five_sessions_is_flagged_failed()
    {
        var failed = Series((101m, 102m, 100m, 101m), (101m, 101m, 97m, 99m));
        var held = Series((101m, 102m, 100m, 101m), (101m, 103m, 100m, 102m));

        PatternBacktestCore.Simulate(Sig(0), failed).FailedWithin5.Should().BeTrue();
        PatternBacktestCore.Simulate(Sig(0), held).FailedWithin5.Should().BeFalse();
        PatternBacktestCore.Simulate(Sig(0, kind: EventKind.BearTrap), failed).FailedWithin5.Should().BeFalse("chỉ áp cho các loại phá vỡ lên");
    }

    // ---------------- sinh tín hiệu

    private static List<StockPriceData> Flat(int n, decimal c = 100m) =>
        Enumerable.Range(0, n).Select(i => Bar(i, c, c * 1.004m, c * 0.996m, c)).ToList();

    [Fact]
    public void donchian_signals_are_first_breakout_bars_with_a_twenty_session_cooldown_and_the_agreed_stop()
    {
        var bars = Flat(30);
        bars.Add(Bar(30, 100, 104, 100, 103));   // phá vỡ đầu tiên, trên 100,4
        bars.Add(Bar(31, 103, 105, 102, 104));   // nến thứ hai trên pivot: không tính
        for (var i = 32; i < 40; i++) bars.Add(Bar(i, 100, 100.4m, 99.6m, 100));
        bars.Add(Bar(40, 100, 106, 100, 105));   // phá vỡ khác cách 10 phiên: nằm trong thời gian nghỉ

        var s = PatternBacktestCore.Donchian("TST", bars, 20, EventKind.Donchian20);

        s.Should().ContainSingle();
        s[0].SignalIndex.Should().Be(30);
        s[0].Level.Should().BeApproximately(100.4m, 0.001m);
        s[0].Stop.Should().BeApproximately(99.6m * 0.99m, 0.001m);
    }

    [Fact]
    public void donchian_55_needs_a_longer_history_than_donchian_20()
    {
        var bars = Flat(40);
        bars.Add(Bar(40, 100, 104, 100, 103));

        PatternBacktestCore.Donchian("TST", bars, 20, EventKind.Donchian20).Should().ContainSingle();
        PatternBacktestCore.Donchian("TST", bars, 55, EventKind.Donchian55).Should().BeEmpty();
    }

    [Fact]
    public void a_bear_trap_signal_is_the_return_day_with_the_trap_stop_and_the_support_as_level()
    {
        var closes = Enumerable.Repeat(100m, 30).Concat(new[] { 96m, 97m, 100m, 100m, 100m, 100m, 100m }).ToList();
        var bars = closes.Select((c, i) => Bar(i, c, c * 1.004m, c * 0.996m, c)).ToList();

        var s = PatternBacktestCore.BearTraps("TST", bars).Should().ContainSingle().Subject;

        s.SignalIndex.Should().Be(32, "phiên đóng cửa quay lại trên hỗ trợ");
        s.Level.Should().BeApproximately(99.6m, 0.001m);
        s.Stop.Should().BeApproximately(96m * 0.996m * 0.99m, 0.001m);
        s.Kind.Should().Be(EventKind.BearTrap);
    }

    [Fact]
    public void a_cup_signal_is_the_first_session_the_candidate_has_broken_out()
    {
        // cốc sạch: đà tăng 70 phiên, đáy 72, vành 98, tay cầm xuống 90 rồi vọt lên 99,5 ở nến cuối
        var closes = new List<decimal>();
        void Leg(int n, decimal from, decimal to) { for (var k = 1; k <= n; k++) closes.Add(from + (to - from) * k / n); }
        Leg(70, 60m, 100m); Leg(25, 100m, 72m); Leg(40, 72m, 98m); Leg(7, 98m, 90m); Leg(2, 90m, 91m); closes.Add(99.5m);
        var bars = closes.Select((c, i) => Bar(i, c, c * 1.004m, c * 0.996m, c)).ToList();

        var s = PatternBacktestCore.Cups("TST", bars);

        s.Should().ContainSingle();
        s[0].SignalIndex.Should().Be(bars.Count - 1);
        s[0].Level.Should().BeApproximately(98m * 1.004m, 0.001m);
        s[0].Kind.Should().Be(EventKind.Cup);
    }

    [Theory]
    [InlineData(20, 2)]
    [InlineData(19, 1)]
    public void the_cooldown_between_two_donchian_signals_is_exactly_twenty_sessions(int gap, int expected)
    {
        var bars = Flat(30);
        bars.Add(Bar(30, 100, 104, 100, 103));                                       // phá vỡ đầu tiên
        for (var i = 31; i < 30 + gap; i++) bars.Add(Bar(i, 100, 100.4m, 99.6m, 100));
        bars.Add(Bar(30 + gap, 100, 108, 100, 107));                                 // phá vỡ lần hai (trên đỉnh 104 của lần một)

        PatternBacktestCore.Donchian("TST", bars, 20, EventKind.Donchian20).Should().HaveCount(expected);
    }

    [Fact]
    public void a_long_steady_climb_gives_one_donchian_signal_not_one_per_new_high_even_after_the_cooldown()
    {
        var bars = Flat(25);
        for (var i = 25; i < 80; i++) bars.Add(Bar(i, 100 + (i - 24), 101 + (i - 24), 99 + (i - 24), 100.5m + (i - 24)));   // mỗi phiên một đỉnh mới

        var s = PatternBacktestCore.Donchian("TST", bars, 20, EventKind.Donchian20);

        s.Should().ContainSingle("chỉ nến phá vỡ ĐẦU TIÊN; các nến sau đó vẫn trên pivot nên không phải nến đầu");
        s[0].SignalIndex.Should().Be(25);
    }

    [Fact]
    public void a_close_exactly_at_the_level_is_not_a_failed_breakout()
    {
        var bars = Series((101m, 102m, 100m, 101m), (101m, 101m, 99m, 100m));   // phiên 2 đóng đúng bằng mức 100

        PatternBacktestCore.Simulate(Sig(0), bars).FailedWithin5.Should().BeFalse();
    }

    [Fact]
    public void a_zero_open_is_never_a_fill()
    {
        PatternBacktestCore.Simulate(Sig(0), Series((0m, 3m, 0m, 2m))).Outcome.Should().Be(TradeOutcome.Skipped);
    }

    [Fact]
    public void percentiles_on_a_series_whose_rank_is_not_an_integer_round_up()
    {
        var v = new[] { 10.0, 20, 30, 40, 50, 60, 70 };   // p50 · 7 = 3,5 → hạng 4 → 40

        PatternBacktestCore.Percentile(v, 50).Should().Be(40);
        PatternBacktestCore.Percentile(v, 10).Should().Be(10);
        PatternBacktestCore.Percentile(v, 90).Should().Be(70);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    public void two_signal_dates_merge_into_one_cluster_only_when_at_most_five_days_apart(int days, int clusters)
    {
        var d = new DateTime(2025, 3, 3);
        var rows = new List<TradeResult> { R(EventKind.Cup, d, TradeOutcome.Success), R(EventKind.Cup, d.AddDays(days), TradeOutcome.Trap) };

        PatternBacktestCore.Summarise(EventKind.Cup, rows).DateClusters.Should().Be(clusters);
    }

    // ---------------- thống kê

    private static TradeResult R(EventKind kind, DateTime date, TradeOutcome o, decimal a2 = 0.02m, decimal af = 0.04m, bool failed = false)
        => new(new BacktestSignal("S", kind, 0, date, 100, 95), o, 100, 5, a2, af, 5, failed);

    [Fact]
    public void the_summary_counts_outcomes_skips_failures_and_date_clusters_per_kind()
    {
        var d = new DateTime(2025, 3, 3);
        var rows = new List<TradeResult>
        {
            R(EventKind.Donchian20, d, TradeOutcome.Success),
            R(EventKind.Donchian20, d.AddDays(2), TradeOutcome.Trap, failed: true),   // cùng cụm (cách ≤ 5 ngày)
            R(EventKind.Donchian20, d.AddDays(3), TradeOutcome.Trap),
            R(EventKind.Donchian20, d.AddDays(20), TradeOutcome.Timeout),              // cụm mới
            R(EventKind.Donchian20, d.AddDays(20), TradeOutcome.Skipped),              // bỏ qua: không tính vào n
            R(EventKind.Donchian55, d, TradeOutcome.Success),
        };

        var s = PatternBacktestCore.Summarise(EventKind.Donchian20, rows);

        (s.Episodes, s.Success, s.Trap, s.Timeout, s.Skipped, s.FailedWithin5).Should().Be((4, 1, 2, 1, 1, 1));
        s.DateClusters.Should().Be(2);
        s.MaxSameDate.Should().Be(1);
        s.ByMonth.Should().Equal(("2025-03", 4));
    }

    [Fact]
    public void percentiles_use_the_nearest_rank_and_an_empty_series_has_none()
    {
        var v = new[] { 1.0, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        PatternBacktestCore.Percentile(v, 10).Should().Be(1);
        PatternBacktestCore.Percentile(v, 50).Should().Be(5);
        PatternBacktestCore.Percentile(v, 90).Should().Be(9);
        PatternBacktestCore.Percentile(Array.Empty<double>(), 50).Should().BeNull();
    }

    [Fact]
    public void the_report_has_counts_and_adverse_excursions_but_no_success_percentage()
    {
        var rows = new List<TradeResult> { R(EventKind.Donchian20, new DateTime(2025, 3, 3), TradeOutcome.Success) };
        var text = PatternBacktestCore.Format(new[] { PatternBacktestCore.Summarise(EventKind.Donchian20, rows) }, 30, new DateTime(2023, 1, 1), new DateTime(2026, 1, 1));

        text.Should().Contain("Donchian20").And.Contain("thành công").And.Contain("biên độ đi ngược").And.Contain("chưa đủ phiên");
        text.Should().NotContain("%", "báo cáo chỉ đếm; riêng đơn vị biên độ ghi bằng chữ").And.NotContain("xác suất");
    }
}
