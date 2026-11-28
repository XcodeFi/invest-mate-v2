using FluentAssertions;
using InvestmentApp.Domain.Services;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class BaseRateStatisticsTests
{
    private static readonly DateTime Start = new(2024, 1, 15);

    /// <summary>Mỗi tháng trong <paramref name="months"/> có <paramref name="perMonth"/> lệnh, R lấy từ <paramref name="r"/> theo số thứ tự tháng.</summary>
    private static List<PooledTrade> Clustered(int months, int perMonth, Func<int, decimal> r)
        => Enumerable.Range(0, months)
            .SelectMany(m => Enumerable.Range(0, perMonth).Select(_ => new PooledTrade(Start.AddMonths(m), r(m))))
            .ToList();

    /// <summary>30 tháng, nửa số tháng thắng hết, nửa còn lại thua hết: lệnh cùng tháng giống hệt nhau (phụ thuộc cực đoan).</summary>
    private static List<PooledTrade> PerfectlyClustered() => Clustered(30, 4, m => m % 2 == 0 ? 1m : -1m);

    // ───── cổng ─────

    [Fact]
    public void base_rate_null_when_k_below_15()
    {
        BaseRateStatistics.Compute(14, PerfectlyClustered()).Should().BeNull();
        BaseRateStatistics.Compute(15, PerfectlyClustered()).Should().NotBeNull();
    }

    [Fact]
    public void base_rate_null_when_n_below_30()
    {
        var trades = Clustered(29, 1, m => 1m);   // 29 lệnh trên 29 tháng
        BaseRateStatistics.Compute(20, trades).Should().BeNull();
        BaseRateStatistics.Compute(20, trades.Append(new PooledTrade(Start.AddMonths(29), 1m)).ToList()).Should().NotBeNull();
    }

    [Fact]
    public void base_rate_null_when_effective_months_below_24()
    {
        BaseRateStatistics.Compute(20, Clustered(23, 5, m => 1m)).Should().BeNull();
        BaseRateStatistics.Compute(20, Clustered(24, 5, m => 1m)).Should().NotBeNull();
    }

    [Fact]
    public void gate_failure_names_the_shortfall()
    {
        BaseRateStatistics.GateFailure(7, 100, 30).Should().Be("Chưa đủ mã VN30 có dữ liệu (7/30, cần từ 15)");
        BaseRateStatistics.GateFailure(20, 12, 30).Should().Contain("12").And.Contain("30");
        BaseRateStatistics.GateFailure(20, 100, 10).Should().Contain("10 tháng").And.Contain("24");
        BaseRateStatistics.GateFailure(20, 100, 30).Should().BeNull();
    }

    // ───── bootstrap ─────

    [Fact]
    public void bootstrap_is_deterministic_with_seed()
    {
        var a = BaseRateStatistics.Compute(20, PerfectlyClustered());
        var b = BaseRateStatistics.Compute(20, PerfectlyClustered());

        a.Should().Be(b);
    }

    [Fact]
    public void bootstrap_resamples_whole_months()
    {
        // Nếu lấy mẫu theo từng lệnh thì 120 lệnh cho khoảng hẹp (≈ ±0,09); lấy theo tháng (30 cụm) thì khoảng rộng (≈ ±0,18)
        var stats = BaseRateStatistics.Compute(20, PerfectlyClustered())!;

        (stats.WinRateHigh - stats.WinRateLow).Should().BeGreaterThan(0.30m, "lệnh cùng tháng đi cùng nhau, nên độ bất định là của 30 tháng chứ không phải 120 lệnh");
    }

    [Fact]
    public void ci_wider_than_naive_wilson_when_trades_cluster()
    {
        var trades = PerfectlyClustered();
        var stats = BaseRateStatistics.Compute(20, trades)!;
        var (wLow, wHigh) = ExpectancyCalculator.WilsonInterval(trades.Count(t => t.R > 0m), trades.Count);

        (stats.WinRateHigh - stats.WinRateLow).Should().BeGreaterThan((wHigh - wLow) * 1.5m);
    }

    [Fact]
    public void win_is_r_greater_than_zero()
    {
        var breakEven = BaseRateStatistics.Compute(20, Clustered(30, 2, _ => 0m))!;
        var tinyWin = BaseRateStatistics.Compute(20, Clustered(30, 2, _ => 0.0001m))!;

        breakEven.WinRate.Should().Be(0m, "hoà vốn (R = 0) là thua");
        tinyWin.WinRate.Should().Be(1m);
    }

    [Fact]
    public void point_estimates_are_over_all_trades_and_months_without_trades_stay_as_clusters()
    {
        // 24 tháng có lệnh rải trong 30 tháng (6 tháng trống ở giữa), tháng đầu có nhiều lệnh hơn hẳn
        var trades = new List<PooledTrade>();
        for (var m = 0; m < 30; m++)
        {
            if (m is >= 10 and < 16) continue;   // 6 tháng trống
            var n = m == 0 ? 10 : 2;
            for (var i = 0; i < n; i++) trades.Add(new PooledTrade(Start.AddMonths(m), m % 3 == 0 ? 2m : -1m));
        }

        var stats = BaseRateStatistics.Compute(20, trades)!;

        stats.EffectiveMonths.Should().Be(24);
        stats.TotalMonths.Should().Be(30, "tháng không có lệnh vẫn là một cụm khi lấy mẫu lại");
        stats.N.Should().Be(trades.Count);
        stats.EvR.Should().Be(Math.Round(trades.Sum(t => t.R) / trades.Count, 4), "R trung bình tính trên toàn bộ lệnh, không phải trung bình của các tháng");
        stats.WinRate.Should().Be(Math.Round((decimal)trades.Count(t => t.R > 0m) / trades.Count, 4));
    }

    [Fact]
    public void intervals_bracket_the_estimate_and_stay_within_the_r_range()
    {
        var stats = BaseRateStatistics.Compute(20, Clustered(36, 3, m => m % 5 == 0 ? 2m : -1m))!;

        stats.WinRateLow.Should().BeLessThanOrEqualTo(stats.WinRate);
        stats.WinRateHigh.Should().BeGreaterThanOrEqualTo(stats.WinRate);
        stats.EvRLow.Should().BeLessThanOrEqualTo(stats.EvR);
        stats.EvRHigh.Should().BeGreaterThanOrEqualTo(stats.EvR);
        stats.EvRLow.Should().BeGreaterThanOrEqualTo(-1m);
        stats.EvRHigh.Should().BeLessThanOrEqualTo(2m);
    }

    [Fact]
    public void the_result_does_not_depend_on_the_order_of_the_trades()
    {
        var trades = PerfectlyClustered();
        var shuffled = trades.OrderByDescending(t => t.EntryDate).ThenBy(t => t.R).ToList();

        BaseRateStatistics.Compute(20, shuffled).Should().Be(BaseRateStatistics.Compute(20, trades));
    }
}
