using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

/// <summary>
/// "Đang kích hoạt" = tín hiệu MỚI NHẤT với t ≥ phiên cuối − 2 mà lệnh của nó không bị bỏ qua, chưa đóng và không bị bỏ vì chồng lệnh.
/// Chưa có phiên vào lệnh (d0) thì vào lệnh là ước tính = đóng cửa t; có rồi thì là giá mở d0 và có trạng thái "đang mở, ngày thứ k".
/// </summary>
public class ActiveSignalTests
{
    private const int N = 200;
    private static readonly DateTime Day0 = new(2025, 1, 1);

    // Open ≠ Close ở mọi nến (Open thấp hơn 50 đồng): ước tính vào lệnh theo Open thay vì Close thì test đỏ
    private static List<Bar> Bars() => Enumerable.Range(0, N)
        .Select(i => new Bar(Day0.AddDays(i), 29_950m + i, 30_050m + i, 29_900m + i, 30_000m + i)).ToList();

    private static readonly MarketRules Hose = MarketRules.For("HOSE");

    private static List<decimal?> Atr() => Enumerable.Repeat((decimal?)1_000m, N).ToList();

    private static SimulatedTrade OpenTrade(int t, decimal entry = 30_000m)
        => new(t, Day0.AddDays(t), Day0.AddDays(t + 1), entry, entry - 2_000m, entry + 4_000m, null, null, null, null, false, null);

    private static SimulatedTrade DoneTrade(int t)
        => new(t, Day0.AddDays(t), Day0.AddDays(t + 1), 30_000m, 28_000m, 34_000m, Day0.AddDays(t + 5), 34_000m, "target", 1.9m, false, null);

    private static SimulatedTrade SkippedTrade(int t)
        => new(t, Day0.AddDays(t), null, null, null, null, null, null, null, null, true, "open-near-ceiling");

    private static ActivePlan? Find(int[] signals, params SimulatedTrade[] trades)
        => ActiveSignal.Find(Bars(), Atr(), signals, trades, Hose);

    [Fact]
    public void no_signal_in_the_last_three_sessions_means_nothing_is_active()
    {
        // phiên cuối = 199; cửa sổ 197..199; tín hiệu 196 nằm ngoài
        Find(new[] { 100, 196 }, OpenTrade(196)).Should().BeNull();
    }

    [Fact]
    public void a_signal_on_the_last_session_has_an_estimated_entry_at_its_close()
    {
        var plan = Find(new[] { 199 })!;

        plan.Status.Should().Be("moi");
        plan.EntryIsEstimate.Should().BeTrue();
        plan.Entry.Should().Be(30_199m, "ước tính = đóng cửa phiên tín hiệu");
        plan.Stop.Should().Be(28_199m, "vào − 2 × ATR(t)");
        plan.Target.Should().Be(34_199m, "vào + 2R, 1R = 2 × ATR");
        plan.DayInTrade.Should().BeNull();
        plan.SignalDate.Should().Be(Day0.AddDays(199));
    }

    [Theory]
    [InlineData(1)]    // đóng ở trần
    [InlineData(-1)]   // đóng ở sàn
    public void a_signal_on_the_last_session_that_closed_at_the_limit_is_not_active(int direction)
    {
        // Cùng luật với mô phỏng: phiên tín hiệu đóng ở trần/sàn thì bỏ qua lệnh, nên không thể "đang kích hoạt" với vào lệnh ước tính
        var bars = Bars();
        var prev = bars[198].Close;
        var limitClose = prev * (1 + direction * Hose.Limit);
        bars[199] = new Bar(bars[199].Date, prev, Math.Max(prev, limitClose), Math.Min(prev, limitClose), limitClose);

        ActiveSignal.Find(bars, Atr(), new[] { 199 }, Array.Empty<SimulatedTrade>(), Hose).Should().BeNull();
    }

    [Fact]
    public void a_signal_on_the_last_session_is_ignored_while_an_earlier_trade_is_still_open()
    {
        Find(new[] { 150, 199 }, OpenTrade(150)).Should().BeNull("đang giữ lệnh thì tín hiệu mới bị bỏ vì chồng lệnh");
    }

    [Fact]
    public void a_signal_on_the_last_session_counts_when_the_earlier_trade_is_already_closed()
    {
        Find(new[] { 150, 199 }, DoneTrade(150))!.Status.Should().Be("moi");
    }

    [Theory]
    [InlineData(198, 1)]   // d0 = 199 là ngày thứ 1
    [InlineData(197, 2)]   // d0 = 198 → ngày thứ 2 tại phiên 199
    public void an_open_trade_shows_entry_from_the_trade_and_the_day_number(int signal, int day)
    {
        var plan = Find(new[] { signal }, OpenTrade(signal, entry: 30_300m))!;

        plan.Status.Should().Be("dang-mo");
        plan.EntryIsEstimate.Should().BeFalse();
        plan.Entry.Should().Be(30_300m, "giá mở d0 của lệnh, không phải ước tính");
        plan.Stop.Should().Be(28_300m);
        plan.Target.Should().Be(34_300m);
        plan.DayInTrade.Should().Be(day);
    }

    [Fact]
    public void a_skipped_signal_is_not_active_but_an_older_one_in_the_window_still_can_be()
    {
        var plan = Find(new[] { 197, 199 }, OpenTrade(197), SkippedTrade(199));

        // 199 bị bỏ (trần), 197 đang mở: 197 là cái kích hoạt
        plan.Should().NotBeNull();
        plan!.SignalDate.Should().Be(Day0.AddDays(197));
        Find(new[] { 198 }, SkippedTrade(198)).Should().BeNull();
    }

    [Fact]
    public void a_closed_trade_is_not_active()
    {
        Find(new[] { 198 }, DoneTrade(198)).Should().BeNull();
    }

    [Fact]
    public void a_signal_ignored_for_overlap_has_no_trade_and_is_not_active()
    {
        // 198 có d0 (199) nhưng không có lệnh nào của nó: bị bỏ vì chồng lệnh
        Find(new[] { 150, 198 }, OpenTrade(150)).Should().BeNull();
    }

    [Fact]
    public void the_newest_eligible_signal_wins()
    {
        var plan = Find(new[] { 197, 198 }, OpenTrade(197), OpenTrade(198))!;

        plan.SignalDate.Should().Be(Day0.AddDays(198));
    }

    [Fact]
    public void missing_atr_at_the_signal_gives_no_plan()
    {
        var atr = Atr();
        atr[199] = null;

        ActiveSignal.Find(Bars(), atr, new[] { 199 }, Array.Empty<SimulatedTrade>(), Hose).Should().BeNull();
    }
}
