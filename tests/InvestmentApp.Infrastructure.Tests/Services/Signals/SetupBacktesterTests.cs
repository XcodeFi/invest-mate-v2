using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

/// <summary>
/// Mô phỏng lệnh theo luật thị trường Việt Nam. Nền chung: giá 30.000, ATR 1.000 → vào 30.000, cắt lỗ 28.000, 1R = 2.000, mục tiêu 34.000.
/// Phiên d0 = tín hiệu + 1; mua ở d0 chưa bán được tới hết d2 (T+2,5), từ d3 mới kiểm tra bình thường.
/// Luật mặc định của test là UPCOM (biên 15%) để các cú gap/khoá sàn không vướng giới hạn giá; test về giới hạn dùng sàn cụ thể.
/// </summary>
public class SetupBacktesterTests
{
    private static readonly DateTime Day0 = new(2025, 1, 1);
    private static readonly MarketRules Upcom = MarketRules.For("UPCOM");
    private const decimal P = 30_000m;

    private static Bar Flat(int i, decimal price = P) => new(Day0.AddDays(i), price, price, price, price);

    /// <summary>Chuỗi n nến phẳng ở giá P; <paramref name="edit"/> sửa từng nến theo chỉ số.</summary>
    private static List<Bar> Bars(int n, Func<int, Bar?>? edit = null)
        => Enumerable.Range(0, n).Select(i => edit?.Invoke(i) ?? Flat(i)).ToList();

    private static Bar Candle(int i, decimal open, decimal high, decimal low, decimal close) => new(Day0.AddDays(i), open, high, low, close);

    private static List<decimal?> Atr(int n, decimal atr = 1_000m) => Enumerable.Repeat((decimal?)atr, n).ToList();

    private static List<SimulatedTrade> Run(List<Bar> bars, int[] signals, MarketRules? rules = null, decimal atr = 1_000m)
        => SetupBacktester.Simulate(bars, signals, Atr(bars.Count, atr), rules ?? Upcom);

    // ───── vào lệnh, cắt lỗ, mục tiêu ─────

    [Fact]
    public void entry_is_the_open_of_d0_not_the_signal_close_and_stop_target_follow_atr_of_the_signal_session()
    {
        // tín hiệu ở phiên 1 (đóng 30.000); d0 = phiên 2 mở 30.500
        var bars = Bars(30, i => i == 2 ? Candle(2, 30_500m, 30_500m, 30_500m, 30_500m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.Entry.Should().Be(30_500m, "mua ở giá mở cửa phiên sau tín hiệu, không phải giá đóng cửa phiên tín hiệu");
        t.Stop.Should().Be(28_500m, "vào − 2 × ATR(t)");
        t.Target.Should().Be(34_500m, "vào + 2R, 1R = 2 × ATR");
        t.EntryDate.Should().Be(Day0.AddDays(2));
        t.SignalDate.Should().Be(Day0.AddDays(1));
    }

    [Fact]
    public void stop_not_positive_is_skipped()
    {
        var bars = Bars(30);

        var t = Run(bars, new[] { 1 }, atr: 20_000m).Single();   // 30.000 − 40.000 < 0

        t.Skipped.Should().BeTrue();
        t.SkipReason.Should().Be("stop-not-positive");
        t.Entry.Should().BeNull();
    }

    [Fact]
    public void zero_risk_is_skipped_not_divided()
    {
        var bars = Bars(30);

        var t = Run(bars, new[] { 1 }, atr: 0m).Single();

        t.Skipped.Should().BeTrue();
        t.SkipReason.Should().Be("zero-risk");
    }

    [Fact]
    public void a_signal_on_the_last_session_has_no_d0_yet_and_makes_no_trade()
    {
        var bars = Bars(10);

        Run(bars, new[] { 9 }).Should().BeEmpty();
    }

    // ───── thứ tự kiểm tra từ d3 ─────

    [Fact]
    public void stop_is_hit_at_the_stop_price_from_d3()
    {
        var bars = Bars(40, i => i == 5 ? Candle(5, 29_500m, 29_600m, 27_500m, 28_000m) : null);   // d3 = phiên 5, chạm cắt lỗ

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("stop");
        t.Exit.Should().Be(28_000m);
        t.ExitDate.Should().Be(Day0.AddDays(5));
        t.R!.Value.Should().BeNegative();
    }

    [Fact]
    public void target_is_hit_at_the_target_price_from_d3()
    {
        var bars = Bars(40, i => i == 5 ? Candle(5, 30_500m, 34_500m, 30_400m, 34_200m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("target");
        t.Exit.Should().Be(34_000m);
    }

    [Fact]
    public void same_session_stop_and_target_counts_stop()
    {
        var bars = Bars(40, i => i == 5 ? Candle(5, 30_000m, 35_000m, 27_000m, 31_000m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("stop", "không biết cái nào chạm trước thì tính xấu nhất");
        t.Exit.Should().Be(28_000m);
    }

    [Fact]
    public void gap_below_stop_exits_at_open()
    {
        var bars = Bars(40, i => i == 5 ? Candle(5, 27_000m, 27_400m, 26_500m, 27_000m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("gap");
        t.Exit.Should().Be(27_000m, "mở cửa đã dưới cắt lỗ thì bán ở giá mở, không phải giá cắt lỗ");
    }

    // ───── T+2,5 ─────

    [Theory]
    [InlineData(2)]   // d0: cắt lỗ chạm trong chính phiên mua
    [InlineData(3)]   // d1
    [InlineData(4)]   // d2
    public void t25_stop_touch_in_d0_to_d2_exits_at_the_open_of_d3(int touchIndex)
    {
        var bars = Bars(40, i => i == touchIndex ? Candle(i, 30_000m, 30_000m, 27_900m, 28_500m)
                              : i == 5 ? Candle(5, 29_400m, 29_800m, 29_300m, 29_500m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("t25");
        t.Exit.Should().Be(29_400m, "chưa bán được trước d3 nên thoát ở giá mở d3, dù đã chạm cắt lỗ");
        t.ExitDate.Should().Be(Day0.AddDays(5));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void t25_target_touch_in_d0_to_d2_exits_at_the_open_of_d3(int touchIndex)
    {
        var bars = Bars(40, i => i == touchIndex ? Candle(i, 30_000m, 34_500m, 30_000m, 33_000m)
                              : i == 5 ? Candle(5, 30_700m, 30_900m, 30_500m, 30_600m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("t25");
        t.Exit.Should().Be(30_700m, "mục tiêu chạm trong d0..d2 cũng chỉ thoát ở giá mở d3 (mô hình thận trọng)");
    }

    [Fact]
    public void both_stop_and_target_touched_in_d0_to_d2_exits_at_the_d3_open()
    {
        var bars = Bars(40, i => i == 2 ? Candle(2, 30_000m, 34_500m, 30_000m, 33_000m)
                              : i == 4 ? Candle(4, 30_000m, 30_000m, 27_900m, 28_500m)
                              : i == 5 ? Candle(5, 29_900m, 30_000m, 29_800m, 29_900m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("t25");
        t.Exit.Should().Be(29_900m);
    }

    [Fact]
    public void nothing_touched_in_d0_to_d2_does_not_force_an_exit_at_d3()
    {
        var bars = Bars(40);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("expiry", "không chạm gì thì chạy tới hết 20 phiên");
    }

    // ───── khoá sàn ─────

    [Fact]
    public void floor_locked_exit_moves_to_next_unlocked_open()
    {
        // HOSE (7%), 30.000: sàn 27.900. d3 (phiên 5) mở 27.900 ≤ cắt lỗ → gap, nhưng cả phiên nằm ở sàn (không bán được).
        // d4 (phiên 6) cũng sàn 25.947; d5 (phiên 7) mở 26.000, có thể bán → thoát ở 26.000.
        var hose = MarketRules.For("HOSE");
        var bars = Bars(40, i => i == 5 ? Candle(5, 27_900m, 27_900m, 27_900m, 27_900m)
                              : i == 6 ? Candle(6, 25_947m, 25_947m, 25_947m, 25_947m)
                              : i == 7 ? Candle(7, 26_000m, 26_500m, 25_900m, 26_200m)
                              : i > 7 ? Flat(i, 26_200m) : null);

        var t = Run(bars, new[] { 1 }, hose).Single();

        t.ExitReason.Should().Be("floor-delay");
        t.Exit.Should().Be(26_000m);
        t.ExitDate.Should().Be(Day0.AddDays(7));
    }

    [Fact]
    public void the_d3_open_itself_floor_locked_applies_the_floor_rule()
    {
        var hose = MarketRules.For("HOSE");
        // d1 (phiên 3) chạm cắt lỗ → chờ thoát ở d3 (phiên 5); nhưng d3 nằm ở sàn → dời tới phiên không khoá đầu tiên (phiên 6)
        var bars = Bars(40, i => i == 3 ? Candle(3, 30_000m, 30_000m, 27_990m, 28_100m)
                              : i == 4 ? Flat(4, 28_100m)
                              : i == 5 ? Candle(5, 26_130m, 26_130m, 26_130m, 26_130m)   // sàn của 28.100: 26.133
                              : i == 6 ? Candle(6, 26_500m, 26_800m, 26_400m, 26_600m)
                              : i > 6 ? Flat(i, 26_600m) : null);

        var t = Run(bars, new[] { 1 }, hose).Single();

        t.ExitReason.Should().Be("floor-delay");
        t.Exit.Should().Be(26_500m);
        t.ExitDate.Should().Be(Day0.AddDays(6));
    }

    [Fact]
    public void a_trade_still_floor_locked_at_the_end_of_data_stays_open()
    {
        var hose = MarketRules.For("HOSE");
        var bars = Bars(7, i => i == 5 ? Candle(5, 27_900m, 27_900m, 27_900m, 27_900m)
                             : i == 6 ? Candle(6, 25_947m, 25_947m, 25_947m, 25_947m) : null);

        var t = Run(bars, new[] { 1 }, hose).Single();

        t.ExitDate.Should().BeNull();
        t.IsOpen.Should().BeTrue();
    }

    // ───── hết hạn, chồng lệnh, đang mở ─────

    [Fact]
    public void expiry_exits_at_close_of_d19()
    {
        // d0 = phiên 2 → d19 = phiên 21. Đóng cửa phiên 21 là 30.700 (không chạm gì); phiên 22 đóng 31.000 không được dùng.
        var bars = Bars(40, i => i == 21 ? Candle(21, 30_000m, 30_800m, 29_900m, 30_700m)
                              : i == 22 ? Candle(22, 31_000m, 31_000m, 31_000m, 31_000m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.ExitReason.Should().Be("expiry");
        t.Exit.Should().Be(30_700m);
        t.ExitDate.Should().Be(Day0.AddDays(21));
    }

    [Fact]
    public void no_overlap_within_setup()
    {
        // Lệnh 1: tín hiệu 1, vào phiên 2, hết hạn ở phiên 21. Tín hiệu 3 nằm trong lúc đang mở → bỏ. Tín hiệu 30 sau khi đóng → nhận.
        var bars = Bars(60);

        var trades = Run(bars, new[] { 1, 3, 30 });

        trades.Select(t => t.SignalDate).Should().Equal(Day0.AddDays(1), Day0.AddDays(30));
    }

    [Fact]
    public void a_signal_on_the_exit_session_itself_is_accepted_but_the_session_before_is_not()
    {
        var bars = Bars(60);   // lệnh 1 thoát ở phiên 21 (close d19)

        Run(bars, new[] { 1, 21 }).Select(t => t.SignalDate.Day).Should().Equal(2, 22);   // phiên 21 → nhận (vào ở phiên 22)
        Run(bars, new[] { 1, 20 }).Should().ContainSingle("phiên 20 còn đang giữ lệnh 1");
    }

    [Fact]
    public void a_skipped_signal_does_not_block_the_next_one()
    {
        // tín hiệu 1 bị bỏ vì mở d0 sát trần; tín hiệu 3 vẫn được xét
        var hose = MarketRules.For("HOSE");
        var bars = Bars(60, i => i == 2 ? Candle(2, 31_900m, 31_900m, 31_900m, 31_900m) : null);   // +6,33% ≥ ngưỡng 6%−slack

        var trades = Run(bars, new[] { 1, 3 }, hose);

        trades.Should().HaveCount(2);
        trades[0].Skipped.Should().BeTrue();
        trades[1].Skipped.Should().BeFalse();
    }

    [Fact]
    public void open_trade_at_end_not_counted()
    {
        var bars = Bars(10);   // d0 = 2, nhưng dữ liệu hết ở phiên 9: chưa thoát

        var trades = Run(bars, new[] { 1 });
        var summary = SetupBacktester.Summarize(trades);

        trades.Single().IsOpen.Should().BeTrue();
        trades.Single().ExitDate.Should().BeNull();
        trades.Single().R.Should().BeNull();
        summary.Completed.Should().Be(0, "lệnh chưa đóng không vào thống kê");
        summary.Open.Should().Be(1);
        summary.Skipped.Should().Be(0);
    }

    [Fact]
    public void open_trade_at_end_blocks_later_signals_but_nothing_else_counts()
    {
        var bars = Bars(12);

        var trades = Run(bars, new[] { 1, 6 });

        trades.Should().ContainSingle("tín hiệu 6 rơi vào lúc lệnh 1 còn mở");
    }

    // ───── bỏ qua lệnh theo giới hạn giá ─────

    [Theory]
    [InlineData("HOSE")]
    [InlineData("HNX")]
    [InlineData("UPCOM")]
    public void entry_skipped_when_open_near_ceiling(string exchange)
    {
        var rules = MarketRules.For(exchange);
        var edge = P * (1 + rules.Limit - 0.01m - rules.Slack(P));
        var atEdge = Bars(40, i => i == 2 ? Candle(2, edge, edge, edge, edge) : null);
        var below = Bars(40, i => i == 2 ? Candle(2, edge - 50m, edge - 50m, edge - 50m, edge - 50m) : null);

        var skipped = Run(atEdge, new[] { 1 }, rules).Single();
        var taken = Run(below, new[] { 1 }, rules).Single();

        skipped.Skipped.Should().BeTrue();
        skipped.SkipReason.Should().Be("open-near-ceiling");
        taken.Skipped.Should().BeFalse();
    }

    [Theory]
    [InlineData("HOSE")]
    [InlineData("HNX")]
    [InlineData("UPCOM")]
    public void entry_skipped_when_signal_session_closed_at_limit(string exchange)
    {
        var rules = MarketRules.For(exchange);
        var ceiling = P * (1 + rules.Limit);
        var floor = P * (1 - rules.Limit);
        var up = Bars(40, i => i == 1 ? Candle(1, P, ceiling, P, ceiling) : i == 2 ? Flat(2, ceiling) : null);
        var down = Bars(40, i => i == 1 ? Candle(1, P, P, floor, floor) : i == 2 ? Flat(2, floor) : null);
        var calm = Bars(40, i => i == 1 ? Candle(1, P, P * 1.03m, P, P * 1.03m) : i == 2 ? Flat(2, P * 1.03m) : null);

        var upTrade = Run(up, new[] { 1 }, rules).Single();
        var downTrade = Run(down, new[] { 1 }, rules).Single();
        var calmTrade = Run(calm, new[] { 1 }, rules).Single();

        upTrade.SkipReason.Should().Be("signal-at-limit");
        downTrade.SkipReason.Should().Be("signal-at-limit");
        calmTrade.Skipped.Should().BeFalse();
    }

    // ───── chi phí ─────

    [Fact]
    public void costs_applied_as_percent_of_price()
    {
        // Vào 30.000, thoát 34.000 (mục tiêu). Phí 0,15% mỗi chiều, thuế bán 0,1%, trượt giá 0,1% mỗi chiều.
        // lãi/cp = 34.000 × (1 − 0,0015 − 0,001 − 0,001) − 30.000 × (1 + 0,0015 + 0,001) = 33.881 − 30.075 = 3.806; 1R = 2.000
        var bars = Bars(40, i => i == 5 ? Candle(5, 30_500m, 34_500m, 30_400m, 34_200m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.Exit.Should().Be(34_000m);
        t.R.Should().Be(1.903m);
    }

    [Fact]
    public void a_stop_loss_costs_more_than_one_r()
    {
        // thoát 28.000: 28.000 × 0,9965 − 30.075 = 27.902 − 30.075 = −2.173 → −1,0865R (chi phí làm lỗ sâu hơn đúng 1R)
        var bars = Bars(40, i => i == 5 ? Candle(5, 29_500m, 29_600m, 27_500m, 28_000m) : null);

        var t = Run(bars, new[] { 1 }).Single();

        t.R.Should().Be(-1.0865m);
    }

    [Fact]
    public void signals_are_taken_in_order_and_trades_come_back_in_signal_order()
    {
        var bars = Bars(120);

        var trades = Run(bars, new[] { 1, 40, 80 });

        trades.Select(t => t.SignalDate).Should().BeInAscendingOrder();
        trades.Should().HaveCount(3);
    }
}
