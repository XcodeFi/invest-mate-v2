using FluentAssertions;
using InvestmentApp.Application.MarketData.Trend;

namespace InvestmentApp.Application.Tests.MarketData.Trend;

/// <summary>Đường trung bình đơn giản, kênh Donchian và đỉnh/đáy 52 tuần: hàm thuần, thiếu dữ liệu thì null chứ không bao giờ là 0.</summary>
public class PriceStructureTests
{
    private static List<decimal> D(params decimal[] v) => v.ToList();
    private static List<decimal?> N(params decimal?[] v) => v.ToList();

    [Fact]
    public void Sma_averages_the_last_period_values_and_is_null_before_the_window_fills()
    {
        PriceStructure.Sma(D(1, 2, 3, 4, 5), 3).Should().Equal(N(null, null, 2m, 3m, 4m));
    }

    [Fact]
    public void Sma_with_fewer_values_than_the_period_is_all_null_never_zero()
    {
        PriceStructure.Sma(D(1, 2), 3).Should().Equal(N(null, null));
        PriceStructure.Sma(D(), 3).Should().BeEmpty();
    }

    [Fact]
    public void PriorHighest_looks_at_the_previous_n_sessions_and_excludes_the_current_one()
    {
        // phiên 2: cao nhất của phiên 0 và 1 = 5 (không tính phiên 2 là 3); phiên 3: cao nhất của phiên 1 và 2 = 5
        PriceStructure.PriorHighest(D(1, 5, 3, 2), 2).Should().Equal(N(null, null, 5m, 5m));
    }

    [Fact]
    public void PriorHighest_lets_a_breakout_exceed_the_channel()
    {
        var highs = D(10, 11, 12, 20);

        var channel = PriceStructure.PriorHighest(highs, 3);

        channel[3].Should().Be(12m);
        highs[3].Should().BeGreaterThan(channel[3]!.Value);
    }

    [Fact]
    public void PriorLowest_looks_at_the_previous_n_sessions_and_excludes_the_current_one()
    {
        PriceStructure.PriorLowest(D(4, 2, 6, 1), 2).Should().Equal(N(null, null, 2m, 2m));
    }

    [Fact]
    public void Week52_needs_a_full_250_sessions_otherwise_both_are_null()
    {
        var highs = Enumerable.Range(0, 249).Select(i => 100m + i).ToList();
        var lows = Enumerable.Range(0, 249).Select(i => 90m + i).ToList();

        var (high, low) = PriceStructure.Week52(highs, lows);

        high.Should().BeNull();
        low.Should().BeNull();
    }

    [Fact]
    public void Week52_takes_the_max_high_and_min_low_of_the_last_250_sessions_only()
    {
        var highs = Enumerable.Range(0, 300).Select(i => i == 10 ? 9_999m : 100m + i).ToList();   // 9.999 nằm ngoài cửa sổ
        var lows = Enumerable.Range(0, 300).Select(i => i == 20 ? 1m : 90m + i).ToList();        // 1 nằm ngoài cửa sổ

        var (high, low) = PriceStructure.Week52(highs, lows);

        high.Should().Be(399m);
        low.Should().Be(140m);   // phiên 50 là phiên đầu của cửa sổ: 90 + 50
    }

    [Fact]
    public void Week52_with_exactly_250_sessions_uses_all_of_them()
    {
        var highs = Enumerable.Range(0, 250).Select(i => 100m + i).ToList();
        var lows = Enumerable.Range(0, 250).Select(i => 90m + i).ToList();

        var (high, low) = PriceStructure.Week52(highs, lows);

        high.Should().Be(349m);
        low.Should().Be(90m);
    }
}
