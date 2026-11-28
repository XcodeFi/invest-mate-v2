using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

/// <summary>
/// Biên độ giá và bước giá theo sàn. Chuỗi đã điều chỉnh không nằm đúng trên bước giá, nên mọi phép so với trần/sàn dùng dung sai
/// slack = max(0,001; 1 bước giá ÷ giá) thay vì so sánh tuyệt đối.
/// </summary>
public class MarketRulesTests
{
    [Theory]
    [InlineData("HOSE", 0.07)]
    [InlineData("hose", 0.07)]
    [InlineData("HSX", 0.07)]
    [InlineData("HNX", 0.10)]
    [InlineData("UPCOM", 0.15)]
    [InlineData("upcom", 0.15)]
    [InlineData("Sàn HNX", 0.10)]
    [InlineData(" UPCOM ", 0.15)]
    [InlineData("HOSE (HSX)", 0.07)]
    public void price_limit_by_exchange(string exchange, double limit)
    {
        MarketRules.For(exchange).Limit.Should().Be((decimal)limit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NYSE")]
    public void unknown_exchange_defaults_to_7pct(string? exchange)
    {
        MarketRules.For(exchange).Limit.Should().Be(0.07m);
    }

    [Theory]
    [InlineData(5_000, 10)]
    [InlineData(9_999, 10)]
    [InlineData(10_000, 50)]
    [InlineData(30_000, 50)]
    [InlineData(49_999, 50)]
    [InlineData(50_000, 100)]
    [InlineData(80_000, 100)]
    public void hose_tick_by_price_band(int price, int tick)
    {
        MarketRules.For("HOSE").Tick(price).Should().Be(tick);
    }

    [Theory]
    [InlineData("HNX")]
    [InlineData("UPCOM")]
    public void hnx_and_upcom_tick_is_100(string exchange)
    {
        foreach (var price in new[] { 5_000m, 30_000m, 80_000m })
            MarketRules.For(exchange).Tick(price).Should().Be(100m);
    }

    [Theory]
    [InlineData("HOSE", 5_000, 0.002)]         // 10 / 5.000
    [InlineData("HOSE", 30_000, 0.0016666667)] // 50 / 30.000
    [InlineData("HOSE", 80_000, 0.00125)]      // 100 / 80.000
    [InlineData("HNX", 5_000, 0.02)]           // 100 / 5.000
    [InlineData("HNX", 30_000, 0.0033333333)]
    [InlineData("HNX", 80_000, 0.00125)]
    [InlineData("UPCOM", 5_000, 0.02)]
    [InlineData("UPCOM", 30_000, 0.0033333333)]
    [InlineData("UPCOM", 80_000, 0.00125)]
    public void slack_is_one_tick_over_price(string exchange, int price, double expected)
    {
        MarketRules.For(exchange).Slack(price).Should().BeApproximately((decimal)expected, 0.000001m);
    }

    [Fact]
    public void slack_never_drops_below_0_001()
    {
        // 100.000 đồng: bước 100 → đúng 0,001; giá cao hơn nữa vẫn không thấp hơn sàn 0,001
        MarketRules.For("HOSE").Slack(100_000m).Should().Be(0.001m);
        MarketRules.For("HOSE").Slack(500_000m).Should().Be(0.001m);
    }

    public static IEnumerable<object[]> ExchangeAndPrice()
    {
        foreach (var exchange in new[] { "HOSE", "HNX", "UPCOM" })
            foreach (var price in new[] { 5_000, 30_000, 80_000 })
                yield return new object[] { exchange, price };
    }

    [Theory]
    [MemberData(nameof(ExchangeAndPrice))]
    public void at_limit_tolerance_at_the_boundary(string exchange, int price)
    {
        var r = MarketRules.For(exchange);
        var prev = (decimal)price;
        var edge = prev * (1 + r.Limit - r.Slack(prev));

        r.IsAtLimit(edge, prev).Should().BeTrue("đúng ngưỡng dung sai thì coi là trần");
        r.IsAtLimit(prev * (1 - r.Limit + r.Slack(prev)), prev).Should().BeTrue("đối xứng cho sàn");
        r.IsAtLimit(edge - 1m, prev).Should().BeFalse("thấp hơn ngưỡng dung sai thì không phải trần");
        r.IsAtLimit(prev, prev).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ExchangeAndPrice))]
    public void open_near_ceiling_tolerance_at_the_boundary(string exchange, int price)
    {
        var r = MarketRules.For(exchange);
        var close = (decimal)price;
        var edge = close * (1 + r.Limit - 0.01m - r.Slack(close));

        r.IsOpenNearCeiling(edge, close).Should().BeTrue();
        r.IsOpenNearCeiling(edge - 1m, close).Should().BeFalse();
        r.IsOpenNearCeiling(close, close).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ExchangeAndPrice))]
    public void floor_lock_tolerance_at_the_boundary(string exchange, int price)
    {
        var r = MarketRules.For(exchange);
        var prev = (decimal)price;
        var edge = prev * (1 - r.Limit) * (1 + r.Slack(prev));

        r.IsFloorLocked(edge, prev).Should().BeTrue("cao nhất phiên chỉ bằng sàn cộng dung sai: không bán được");
        r.IsFloorLocked(edge + 1m, prev).Should().BeFalse("có giá cao hơn sàn quá dung sai: có thể bán");
        r.IsFloorLocked(prev, prev).Should().BeFalse();
    }

    [Fact]
    public void the_old_flat_0_001_tolerance_would_have_missed_a_limit_close_at_80k_hose()
    {
        // 80.000 → trần 85.600 (HOSE). Đóng cửa 85.500 (thấp hơn trần 1 bước) tăng 0,06875: với dung sai phẳng 0,001 (ngưỡng 0,069)
        // chưa tới ngưỡng, dung sai theo bước giá (0,00125, ngưỡng 0,06875) thì bắt được.
        var r = MarketRules.For("HOSE");

        r.IsAtLimit(85_500m, 80_000m).Should().BeTrue();
    }
}
