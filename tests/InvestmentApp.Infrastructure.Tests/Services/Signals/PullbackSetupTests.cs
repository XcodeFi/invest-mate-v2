using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class PullbackSetupTests
{
    private const int N = 200;

    /// <summary>Giá trị chỉ báo và nến của một phiên; mặc định là một phiên THOẢ mọi điều kiện.</summary>
    private sealed record Session(decimal Low = 30_400m, decimal Close = 30_500m, decimal Ema20 = 30_000m, decimal Ema50 = 29_000m,
        decimal Rsi = 48m, decimal Adx = 25m, decimal Atr = 1_000m);

    /// <summary>Phiên không thoả (đóng cửa dưới EMA20), dùng làm nền.</summary>
    private static readonly Session Off = new(Close: 29_500m);

    private static (IReadOnlyList<Bar> Bars, IndicatorSeries S) Build(Func<int, Session> at)
    {
        var bars = new List<Bar>();
        var ema20 = new List<decimal?>(); var ema50 = new List<decimal?>();
        var rsi = new List<decimal?>(); var adx = new List<decimal?>(); var atr = new List<decimal?>();
        for (var i = 0; i < N; i++)
        {
            var s = at(i);
            bars.Add(new Bar(new DateTime(2025, 1, 1).AddDays(i), 30_000m, Math.Max(s.Low, s.Close) + 100m, s.Low, s.Close));
            ema20.Add(s.Ema20); ema50.Add(s.Ema50); rsi.Add(s.Rsi); adx.Add(s.Adx); atr.Add(s.Atr);
        }
        return (bars, new IndicatorSeries(ema20, ema50, rsi, adx, atr));
    }

    [Fact]
    public void signal_only_on_first_session_condition_true()
    {
        // đúng ở 120..125 rồi sai, rồi đúng lại ở 150..151: hai tín hiệu, mỗi cụm đúng một lần ở phiên đầu
        var (bars, s) = Build(i => i is (>= 120 and <= 125) or 150 or 151 ? new Session() : Off);

        PullbackSetup.FindSignals(bars, s).Should().Equal(120, 150);
    }

    [Fact]
    public void no_signal_before_the_warm_up_of_100_sessions()
    {
        // cụm bắt đầu ở 90 (trước warm-up) kéo dài tới 110: phiên đầu 90 < 100 nên không tín hiệu, và 100 không phải phiên đầu
        var (bars, s) = Build(i => i is >= 90 and <= 110 ? new Session() : Off);

        PullbackSetup.FindSignals(bars, s).Should().BeEmpty();
    }

    [Fact]
    public void a_signal_exactly_at_index_100_is_allowed_when_99_does_not_hold()
    {
        var (bars, s) = Build(i => i is >= 100 and <= 105 ? new Session() : Off);

        PullbackSetup.FindSignals(bars, s).Should().Equal(100);
    }

    [Fact]
    public void no_signal_at_index_99_even_when_it_is_the_first_to_hold()
    {
        var (bars, s) = Build(i => i == 99 ? new Session() : Off);

        PullbackSetup.FindSignals(bars, s).Should().BeEmpty();
    }

    [Theory]
    [InlineData("ema-order")]      // EMA20 <= EMA50
    [InlineData("adx")]            // ADX < 20
    [InlineData("low-far")]        // |Low − EMA20| > ATR
    [InlineData("low-far-below")]  // đối xứng phía dưới
    [InlineData("close-below")]    // Close <= EMA20
    [InlineData("rsi-low")]        // RSI < 40
    [InlineData("rsi-high")]       // RSI > 55
    public void each_condition_is_required(string broken)
    {
        var broke = broken switch
        {
            "ema-order" => new Session(Ema50: 30_000m),
            "adx" => new Session(Adx: 19.9m),
            "low-far" => new Session(Low: 31_001m, Close: 31_500m),
            "low-far-below" => new Session(Low: 28_999m),
            "close-below" => new Session(Close: 30_000m),
            "rsi-low" => new Session(Rsi: 39.9m),
            "rsi-high" => new Session(Rsi: 55.1m),
            _ => throw new ArgumentException(broken)
        };
        var (bars, s) = Build(i => i == 150 ? broke : Off);

        PullbackSetup.FindSignals(bars, s).Should().BeEmpty($"thiếu điều kiện: {broken}");
    }

    [Theory]
    [InlineData(40, 20, 31_000)]   // RSI đúng 40, ADX đúng 20, Low cách EMA20 đúng 1 ATR phía trên
    [InlineData(55, 20, 29_000)]   // RSI đúng 55, Low cách EMA20 đúng 1 ATR phía dưới (đóng cửa vẫn trên EMA20)
    public void the_boundaries_are_inclusive(int rsi, int adx, int low)
    {
        var (bars, s) = Build(i => i == 150 ? new Session(Low: low, Close: 30_500m, Rsi: rsi, Adx: adx) : Off);

        PullbackSetup.FindSignals(bars, s).Should().Equal(150);
    }

    [Fact]
    public void indicators_that_are_not_ready_never_give_a_signal()
    {
        var (bars, s) = Build(_ => new Session());
        var noAtr = new IndicatorSeries(s.Ema20, s.Ema50, s.Rsi14, s.Adx14, s.Atr14.Select(_ => (decimal?)null).ToList());
        var noRsi = new IndicatorSeries(s.Ema20, s.Ema50, s.Rsi14.Select(_ => (decimal?)null).ToList(), s.Adx14, s.Atr14);

        PullbackSetup.FindSignals(bars, noAtr).Should().BeEmpty();
        PullbackSetup.FindSignals(bars, noRsi).Should().BeEmpty();
    }
}
