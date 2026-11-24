using FluentAssertions;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Services;

public class ExpectancyCalculatorTests
{
    private static decimal[] Rs(params double[] values) => values.Select(v => (decimal)v).ToArray();

    [Fact]
    public void expectancy_formula()
    {
        // 3 thắng @ +2R, 2 thua @ −1R, 1 hoà @ 0R → winRate 3/5 = 0,6; E = (6 − 2 + 0) / 6 = 0,667R
        var s = ExpectancyCalculator.Compute(Rs(2, 2, 2, -1, -1, 0));

        s.N.Should().Be(6);
        s.Wins.Should().Be(3);
        s.Losses.Should().Be(2);
        s.Draws.Should().Be(1);
        s.WinRate.Should().Be(0.6m);
        s.AvgWinR.Should().Be(2m);
        s.AvgLossR.Should().Be(-1m);
        s.ExpectancyR.Should().BeApproximately(0.6667m, 0.0001m);
        s.ProfitFactorR.Should().Be(3m);   // 6 / 2
    }

    [Fact]
    public void expectancy_includes_draws_in_the_denominator()
    {
        // Hai lệnh hoà kéo kỳ vọng xuống: (2 − 1) / 4 = 0,25, không phải (2 − 1) / 2
        var s = ExpectancyCalculator.Compute(Rs(2, -1, 0, 0.05));

        s.ExpectancyR.Should().BeApproximately(0.2625m, 0.0001m);
        s.Draws.Should().Be(2);
    }

    [Fact]
    public void band_edges_are_draws()
    {
        var s = ExpectancyCalculator.Compute(Rs(0.1, -0.1, 0.11, -0.11));

        s.Draws.Should().Be(2);
        s.Wins.Should().Be(1);
        s.Losses.Should().Be(1);
    }

    [Fact]
    public void pf_null_when_no_losses_but_expectancy_still_computed()
    {
        var s = ExpectancyCalculator.Compute(Rs(1, 2, 0));

        s.ProfitFactorR.Should().BeNull("không có lệnh thua thì PF vô hạn: hiện n/a, không dùng 999,99");
        s.ExpectancyR.Should().Be(1m);
        s.AvgLossR.Should().BeNull();
    }

    [Fact]
    public void pf_zero_when_only_losses()
    {
        var s = ExpectancyCalculator.Compute(Rs(-1, -2));

        s.ProfitFactorR.Should().Be(0m);
        s.AvgWinR.Should().BeNull();
        s.WinRate.Should().Be(0m);
    }

    [Fact]
    public void n0_all_null()
    {
        var s = ExpectancyCalculator.Compute(Array.Empty<decimal>());

        s.N.Should().Be(0);
        s.WinRate.Should().BeNull();
        s.WinRateLow95.Should().BeNull();
        s.WinRateHigh95.Should().BeNull();
        s.AvgWinR.Should().BeNull();
        s.AvgLossR.Should().BeNull();
        s.ExpectancyR.Should().BeNull();
        s.ProfitFactorR.Should().BeNull();
    }

    [Fact]
    public void only_draws_have_no_win_rate_but_a_zero_expectancy()
    {
        var s = ExpectancyCalculator.Compute(Rs(0, 0.05, -0.05));

        s.WinRate.Should().BeNull("không có lệnh thắng hay thua nào để tính tỉ lệ");
        s.WinRateLow95.Should().BeNull();
        s.ExpectancyR.Should().BeApproximately(0m, 0.0001m);
        s.Draws.Should().Be(3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void wilson_bounds_n1_and_n2_finite(int n)
    {
        var s = ExpectancyCalculator.Compute(Enumerable.Repeat(1m, n).ToArray());

        s.WinRateLow95.Should().NotBeNull();
        s.WinRateHigh95.Should().NotBeNull();
        ((double)s.WinRateLow95!.Value).Should().BeInRange(0, 1).And.Subject.Should().NotBe(double.NaN);
        ((double)s.WinRateHigh95!.Value).Should().BeInRange(0, 1);
        s.WinRateLow95.Should().BeLessThan(1m, "một vài lệnh thắng không đủ để chắc chắn 100%");
    }

    [Fact]
    public void wilson_known_value()
    {
        // 8 thắng / 10 quyết định: khoảng Wilson 95% ≈ [0,490; 0,943]
        var rs = Enumerable.Repeat(1m, 8).Concat(Enumerable.Repeat(-1m, 2)).ToArray();

        var s = ExpectancyCalculator.Compute(rs);

        s.WinRateLow95.Should().BeApproximately(0.490m, 0.005m);
        s.WinRateHigh95.Should().BeApproximately(0.943m, 0.005m);
    }

    [Fact]
    public void wilson_uses_decisive_trades_only()
    {
        // 6 thắng + 4 hoà: n quyết định = 6 → tỉ lệ 100% nhưng cận dưới thấp theo cỡ mẫu 6
        var s = ExpectancyCalculator.Compute(Enumerable.Repeat(1m, 6).Concat(Enumerable.Repeat(0m, 4)).ToArray());

        s.WinRate.Should().Be(1m);
        s.WinRateLow95.Should().BeApproximately(0.610m, 0.005m);
    }
}
