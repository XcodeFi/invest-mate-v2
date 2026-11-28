using FluentAssertions;
using InvestmentApp.Domain.Services;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

/// <summary>
/// Thống kê của setup: n &lt; 30 thì chỉ có số lượng (không tỉ lệ thắng, không EV). Từ 30: thắng = R &gt; 0 (R ≤ 0 là thua), tỉ lệ thắng kèm
/// khoảng Wilson 95%, EV theo R kèm khoảng t 95% (mẫu nhỏ, độ lệch chuẩn ước lượng từ mẫu nên dùng phân phối t chứ không phải chuẩn).
/// </summary>
public class SetupStatisticsTests
{
    private static List<decimal> Rs(int wins, int losses, decimal win = 1m, decimal loss = -1m)
        => Enumerable.Repeat(win, wins).Concat(Enumerable.Repeat(loss, losses)).ToList();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(29)]
    public void n_below_30_hides_winrate_and_ev(int n)
    {
        var s = SetupStatistics.Compute(Rs(n, 0));

        s.N.Should().Be(n);
        s.Sufficient.Should().BeFalse();
        s.WinRate.Should().BeNull();
        s.WinRateLow.Should().BeNull();
        s.WinRateHigh.Should().BeNull();
        s.EvR.Should().BeNull();
        s.EvRLow.Should().BeNull();
        s.EvRHigh.Should().BeNull();
        s.AvgWinR.Should().BeNull();
        s.AvgLossR.Should().BeNull();
    }

    [Fact]
    public void n_at_least_30_returns_wilson_and_t_interval()
    {
        // 18 thắng +2R, 12 thua −1R: n = 30, thắng 60%
        var rs = Rs(18, 12, win: 2m, loss: -1m);

        var s = SetupStatistics.Compute(rs);

        s.Sufficient.Should().BeTrue();
        s.N.Should().Be(30);
        s.WinRate.Should().Be(0.6m);
        var (low, high) = ExpectancyCalculator.WilsonInterval(18, 30);
        s.WinRateLow.Should().Be(low);
        s.WinRateHigh.Should().Be(high);
        s.AvgWinR.Should().Be(2m);
        s.AvgLossR.Should().Be(-1m);

        // EV = (18×2 − 12)/30 = 0,8; sd mẫu = sqrt(Σ(x−0,8)²/29) = sqrt((18×1,44 + 12×3,24)/29) = sqrt(64,8/29) = 1,4949
        // nửa khoảng = t(0,975; 29) × sd/√30 = 2,0452 × 1,4949/5,4772 = 0,5582
        s.EvR.Should().BeApproximately(0.8m, 0.0001m);
        s.EvRLow.Should().BeApproximately(0.8m - 0.5582m, 0.002m);
        s.EvRHigh.Should().BeApproximately(0.8m + 0.5582m, 0.002m);
    }

    [Fact]
    public void win_is_net_r_strictly_above_zero_and_zero_counts_as_a_loss()
    {
        // 15 lệnh R = +1, 15 lệnh R = 0 chính xác: thắng 15/30; lệnh hoà (R = 0) là thua
        var rs = Rs(15, 0).Concat(Enumerable.Repeat(0m, 15)).ToList();

        var s = SetupStatistics.Compute(rs);

        s.WinRate.Should().Be(0.5m);
        s.AvgLossR.Should().Be(0m);
    }

    [Fact]
    public void zero_variance_gives_no_ev_interval_but_keeps_the_mean()
    {
        // 30 lệnh cùng R = +1: sd = 0 → khoảng EV không xác định (null), không phải khoảng rộng 0
        var s = SetupStatistics.Compute(Rs(30, 0));

        s.EvR.Should().Be(1m);
        s.EvRLow.Should().BeNull();
        s.EvRHigh.Should().BeNull();
        s.WinRate.Should().Be(1m);
    }

    [Fact]
    public void ev_interval_is_null_when_n_is_below_2_even_if_the_threshold_were_lowered()
    {
        SetupStatistics.EvInterval(new List<decimal> { 1m }).Should().BeNull();
        SetupStatistics.EvInterval(new List<decimal>()).Should().BeNull();
        SetupStatistics.EvInterval(new List<decimal> { 1m, 1m, 1m }).Should().BeNull("sd = 0");
    }

    [Fact]
    public void no_wins_means_no_avg_win_and_no_losses_means_no_avg_loss()
    {
        var allLoss = SetupStatistics.Compute(Rs(0, 30));
        var allWin = SetupStatistics.Compute(Rs(30, 0));

        allLoss.AvgWinR.Should().BeNull();
        allLoss.AvgLossR.Should().Be(-1m);
        allWin.AvgLossR.Should().BeNull();
        allWin.AvgWinR.Should().Be(1m);
    }

    [Theory]
    [InlineData(1, 12.706)]
    [InlineData(5, 2.571)]
    [InlineData(29, 2.045)]
    [InlineData(30, 2.042)]
    [InlineData(40, 2.021)]
    [InlineData(60, 2.000)]
    [InlineData(120, 1.980)]
    [InlineData(1000, 1.962)]
    public void t_quantile_975_matches_the_table(int df, double expected)
    {
        SetupStatistics.TQuantile975(df).Should().BeApproximately((decimal)expected, 0.002m);
    }

    [Fact]
    public void the_t_interval_is_wider_than_a_normal_interval_for_a_small_sample()
    {
        // Tài liệu hoá lý do dùng t: ở n = 30, t(29) = 2,045 > 1,960 nên khoảng rộng hơn khoảng chuẩn ~4%.
        SetupStatistics.TQuantile975(29).Should().BeGreaterThan(1.96m);
    }
}
