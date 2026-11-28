using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

/// <summary>Đường đầy đủ chuỗi nến → chỉ báo → tín hiệu → lệnh trên chuỗi giá tổng hợp: không nhìn trước, và bất biến khi chia tách.</summary>
public class PullbackBacktestTests
{
    private static readonly MarketRules Hose = MarketRules.For("HOSE");

    /// <summary>
    /// Chuỗi giá tổng hợp xác định (seed cố định): xu hướng tăng có nhịp điều chỉnh về EMA20, đủ để sinh nhiều tín hiệu Pullback.
    /// Giá quanh 30.000–80.000 để làm tròn về đồng của EMA/ATR không đáng kể.
    /// </summary>
    internal static List<Bar> Synthetic(int n, int seed, decimal scale = 1m)
    {
        var rnd = new Random(seed);
        var bars = new List<Bar>();
        double price = 30_000;
        var phase = 0.0;
        for (var i = 0; i < n; i++)
        {
            phase += 0.18;
            // trôi tăng + sóng chu kỳ ~35 phiên (nhịp điều chỉnh) + nhiễu
            var drift = 0.0012 + 0.006 * Math.Sin(phase);
            var ret = drift + (rnd.NextDouble() - 0.5) * 0.024;
            var open = price * (1 + (rnd.NextDouble() - 0.5) * 0.006);
            var close = price * (1 + ret);
            var high = Math.Max(open, close) * (1 + rnd.NextDouble() * 0.008);
            var low = Math.Min(open, close) * (1 - rnd.NextDouble() * 0.008);
            bars.Add(new Bar(new DateTime(2023, 1, 2).AddDays(i),
                Math.Round((decimal)open * scale, 2), Math.Round((decimal)high * scale, 2),
                Math.Round((decimal)low * scale, 2), Math.Round((decimal)close * scale, 2)));
            price = close;
        }
        return bars;
    }

    [Fact]
    public void the_synthetic_series_produces_enough_trades_for_the_properties_below_to_mean_something()
    {
        var result = PullbackBacktest.Run(Synthetic(700, seed: 11), Hose);

        result.Signals.Count.Should().BeGreaterThanOrEqualTo(8);
        result.Summary.Completed.Should().BeGreaterThanOrEqualTo(6);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(23)]
    [InlineData(37)]
    public void truncating_series_at_t_does_not_change_trades_closed_by_t(int seed)
    {
        var bars = Synthetic(700, seed);
        var full = PullbackBacktest.Run(bars, Hose);
        var checkedAny = 0;

        for (var t = 120; t < bars.Count; t += 17)
        {
            var truncated = PullbackBacktest.Run(bars.Take(t + 1).ToList(), Hose);
            var cutoff = bars[t].Date;

            var expected = full.Trades.Where(x => x.ExitDate != null && x.ExitDate <= cutoff).ToList();
            var actual = truncated.Trades.Where(x => x.ExitDate != null && x.ExitDate <= cutoff).ToList();

            actual.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering(), $"chuỗi cắt ở phiên {t} phải cho đúng các lệnh đã đóng tới phiên đó");
            checkedAny += expected.Count;

            // Tín hiệu tới phiên t cũng không đổi
            truncated.Signals.Should().Equal(full.Signals.Where(s => s <= t));
        }

        checkedAny.Should().BeGreaterThan(0, "test phải thực sự đối chiếu ít nhất một lệnh đã đóng");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(23)]
    public void a_split_scaled_series_gives_the_same_signals_and_r_within_tick_tolerance(int seed)
    {
        // Chuỗi đã điều chỉnh sau chia tách (ví dụ 2:1) là cùng hình dạng ở giá nhỏ hơn một nửa. Chỉ báo bất biến theo tỉ lệ,
        // nên tín hiệu giống hệt và R lệch không quá dung sai bước giá.
        var unsplit = PullbackBacktest.Run(Synthetic(700, seed, 1.0m), Hose);
        var split = PullbackBacktest.Run(Synthetic(700, seed, 0.5m), Hose);

        split.Signals.Should().Equal(unsplit.Signals);
        var a = unsplit.Trades.Where(t => !t.Skipped).ToList();
        var b = split.Trades.Where(t => !t.Skipped).ToList();
        b.Count.Should().Be(a.Count);
        for (var i = 0; i < a.Count; i++)
        {
            b[i].SignalIndex.Should().Be(a[i].SignalIndex);
            b[i].ExitReason.Should().Be(a[i].ExitReason);
            if (a[i].R is { } r) b[i].R.Should().BeApproximately(r, 0.05m);
        }
    }
}
