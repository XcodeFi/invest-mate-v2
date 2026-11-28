using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Chỉ báo đệ quy (EMA 200, RSI, ADX, ATR, OBV, MACD...) phụ thuộc điểm bắt đầu của dữ liệu, nên cùng một mã cho hai giá trị khác nhau
/// nếu hai trang xin hai khoảng khác nhau. Chỉ báo luôn tính trên cùng một lịch sử cố định; <c>months</c> chỉ quyết định các mốc hỗ trợ,
/// kháng cự, Fibonacci ("trong N tháng gần nhất") và số nến trả về của chuỗi. Provider giả tôn trọng khoảng from/to như provider thật.
/// </summary>
public class TechnicalIndicatorServiceWindowTests
{
    private const int Days = 1000;   // hơn 36 tháng lịch sử
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly TechnicalIndicatorService _sut;
    private readonly List<StockPriceData> _all;

    public TechnicalIndicatorServiceWindowTests()
    {
        _sut = new TechnicalIndicatorService(_marketData.Object);
        _all = Build();
        _marketData.Setup(m => m.GetHistoricalPricesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, DateTime from, DateTime to, CancellationToken _) =>
                _all.Where(p => p.Date >= from.Date && p.Date <= to.Date).ToList());
    }

    /// <summary>Hai chế độ giá: hơn 400 ngày trước quanh 250.000, sau đó quanh 55.000, nên mốc tính trên 36 tháng khác hẳn trên 12 tháng.</summary>
    private static List<StockPriceData> Build()
    {
        var today = DateTime.UtcNow.Date;
        return Enumerable.Range(0, Days).Select(i =>
        {
            var daysAgo = Days - 1 - i;
            var baseLevel = daysAgo > 400 ? 250_000.0 : 55_000.0;
            var amp = daysAgo > 400 ? 20_000.0 : 5_000.0;
            var close = (decimal)Math.Round(baseLevel + amp * Math.Sin(i / 7.0) + 800 * Math.Sin(i / 2.3));
            return new StockPriceData
            {
                Symbol = "FPT", Date = today.AddDays(-daysAgo), Open = close - 150, High = close + 600, Low = close - 600, Close = close,
                Volume = 1_000_000 + (i % 13) * 10_000, IsAdjusted = true, AdjustedAsOf = today
            };
        }).ToList();
    }

    [Fact]
    public async Task indicators_identical_for_months_12_and_36()
    {
        var a12 = await _sut.AnalyzeAsync("FPT", 12);
        var a36 = await _sut.AnalyzeAsync("FPT", 36);
        var aDefault = await _sut.AnalyzeAsync("FPT");

        foreach (var a in new[] { a36, aDefault })
        {
            a.Ema20.Should().Be(a12.Ema20);
            a.Ema50.Should().Be(a12.Ema50);
            a.Ema200.Should().NotBeNull().And.Be(a12.Ema200);
            a.Rsi14.Should().Be(a12.Rsi14);
            a.Adx14.Should().Be(a12.Adx14);
            a.Atr14.Should().Be(a12.Atr14);
            a.MacdLine.Should().Be(a12.MacdLine);
            a.Obv.Should().Be(a12.Obv);
            a.Mfi14.Should().Be(a12.Mfi14);
            a.BollingerMiddle.Should().Be(a12.BollingerMiddle);
            a.StochasticK.Should().Be(a12.StochasticK);
        }
    }

    [Fact]
    public async Task series_last_bar_matches_analysis_and_does_not_depend_on_months()
    {
        var a = await _sut.AnalyzeAsync("FPT");
        var s12 = await _sut.GetSeriesAsync("FPT", 12);
        var s36 = await _sut.GetSeriesAsync("FPT", 36);

        foreach (var s in new[] { s12, s36 })
        {
            s.Ema200.Last().Should().Be(a.Ema200);
            s.Ema50.Last().Should().Be(a.Ema50);
            s.Rsi14.Last().Should().Be(a.Rsi14);
            s.Adx.Last().Should().Be(a.Adx14);
            s.Atr14.Last().Should().Be(a.Atr14);
            s.Obv.Last().Should().Be(a.Obv);
        }
    }

    [Fact]
    public async Task support_resistance_use_requested_window_only()
    {
        var today = DateTime.UtcNow.Date;
        var recent = _all.Where(p => p.Date >= today.AddMonths(-12)).Select(p => p.Close).ToList();

        var a12 = await _sut.AnalyzeAsync("FPT", 12);
        var a36 = await _sut.AnalyzeAsync("FPT", 36);

        a12.Fibonacci.Should().NotBeNull();
        a12.Fibonacci!.SwingHigh.Should().BeLessThanOrEqualTo(recent.Max(), "mốc trong 12 tháng gần nhất");
        a12.Fibonacci.SwingLow.Should().BeGreaterThanOrEqualTo(recent.Min());
        a12.SupportLevels.Concat(a12.ResistanceLevels).Should().OnlyContain(l => l >= recent.Min() && l <= recent.Max());

        a36.Fibonacci.Should().NotBeNull();
        a36.Fibonacci!.SwingHigh.Should().BeGreaterThan(recent.Max(), "36 tháng thì thấy cả mặt bằng giá cũ");
    }

    [Fact]
    public async Task series_output_sliced_to_requested_months()
    {
        var today = DateTime.UtcNow.Date;

        var s12 = await _sut.GetSeriesAsync("FPT", 12);
        var s36 = await _sut.GetSeriesAsync("FPT", 36);

        s12.Dates.First().Should().BeOnOrAfter(today.AddMonths(-12));
        s12.Dates.Last().Should().Be(today);
        s36.Dates.Count.Should().BeGreaterThan(s12.Dates.Count);
        s12.Dates.Count.Should().BeInRange(364, 367);

        var n = s12.Dates.Count;
        foreach (var len in new[]
        {
            s12.Open.Count, s12.High.Count, s12.Low.Count, s12.Close.Count, s12.Volume.Count, s12.Ema20.Count, s12.Ema50.Count, s12.Ema200.Count,
            s12.BollingerUpper.Count, s12.BollingerMiddle.Count, s12.BollingerLower.Count, s12.Rsi14.Count, s12.StochK.Count, s12.StochD.Count,
            s12.MacdLine.Count, s12.MacdSignal.Count, s12.MacdHistogram.Count, s12.Adx.Count, s12.PlusDi.Count, s12.MinusDi.Count,
            s12.Atr14.Count, s12.Obv.Count, s12.Mfi14.Count, s12.VolumeSma20.Count
        })
            len.Should().Be(n);
    }

    [Fact]
    public async Task series_start_of_a_short_window_is_already_warmed_up()
    {
        var s = await _sut.GetSeriesAsync("FPT", 3);

        s.Ema200.First().Should().NotBeNull("EMA 200 đã được tính trên lịch sử dài rồi mới cắt, nên đầu khoảng không trống");
        s.Rsi14.First().Should().NotBeNull();
    }

    [Fact]
    public async Task the_provider_is_asked_for_the_fixed_warm_up_history_whatever_the_months()
    {
        await _sut.AnalyzeAsync("FPT", 3);
        await _sut.GetSeriesAsync("FPT", 3);

        _marketData.Verify(m => m.GetHistoricalPricesAsync("FPT",
            It.Is<DateTime>(d => d <= DateTime.UtcNow.AddMonths(-36).AddDays(1)), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
