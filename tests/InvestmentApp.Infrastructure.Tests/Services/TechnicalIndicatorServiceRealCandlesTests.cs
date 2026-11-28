using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Nguồn lịch sử ngày nay trả nến thật (O/H/L/C khác nhau), đã điều chỉnh, sâu hơn 200 phiên.
/// Khoá những hệ quả tới chỉ báo: EMA200 có giá trị, ATR dùng râu nến, và cờ Adjusted đi tới kết quả.
/// </summary>
public class TechnicalIndicatorServiceRealCandlesTests
{
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceRealCandlesTests() => _sut = new TechnicalIndicatorService(_marketData.Object);

    private void Setup(List<StockPriceData> prices) =>
        _marketData.Setup(m => m.GetHistoricalPricesAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

    private static List<StockPriceData> Bars(int n, bool adjusted, decimal close = 100_000m, decimal halfRange = 2_000m)
    {
        var start = DateTime.UtcNow.Date.AddDays(-n);
        return Enumerable.Range(0, n).Select(i => new StockPriceData
        {
            Symbol = "FPT", Date = start.AddDays(i),
            Open = close, High = close + halfRange, Low = close - halfRange, Close = close,
            Volume = 1_000_000, IsAdjusted = adjusted
        }).ToList();
    }

    private static List<StockPriceData> Trending(int n, bool adjusted)
    {
        var start = DateTime.UtcNow.Date.AddDays(-n);
        return Enumerable.Range(0, n).Select(i =>
        {
            var c = 50_000m + i * 100m + (i % 5) * 300m;
            return new StockPriceData
            {
                Symbol = "FPT", Date = start.AddDays(i), Open = c - 200m, High = c + 500m, Low = c - 600m, Close = c,
                Volume = 1_000_000 + i * 1_000, IsAdjusted = adjusted
            };
        }).ToList();
    }

    [Fact]
    public async Task Analyze_300Sessions_Ema200HasValue()
    {
        Setup(Trending(300, adjusted: true));

        var r = await _sut.AnalyzeAsync("FPT");

        r.Ema200.Should().NotBeNull();
        r.DataPoints.Should().Be(300);
    }

    [Fact]
    public async Task Analyze_Only60Sessions_Ema200StaysNull()
    {
        Setup(Trending(60, adjusted: false));

        var r = await _sut.AnalyzeAsync("FPT");

        r.Ema200.Should().BeNull("không đủ 200 phiên thì không có EMA200, đừng bịa");
    }

    [Fact]
    public async Task Series_Ema200_HasValuesFrom200thSessionAndEndsAtTheAnalysisScalar()
    {
        Setup(Trending(300, adjusted: true));

        var s = await _sut.GetSeriesAsync("FPT");
        var a = await _sut.AnalyzeAsync("FPT");

        s.Ema200.Should().HaveCount(300);
        s.Ema200.Take(199).Should().OnlyContain(v => v == null);
        s.Ema200[199].Should().NotBeNull();
        s.Ema200.Last().Should().Be(a.Ema200, "đường vẽ phải khớp con số trên thẻ");
    }

    [Fact]
    public async Task Series_Ema200_IsAllNullWhenFewerThan200Sessions()
    {
        Setup(Trending(150, adjusted: true));

        var s = await _sut.GetSeriesAsync("FPT");

        s.Ema200.Should().HaveCount(150).And.OnlyContain(v => v == null);
    }

    [Fact]
    public async Task Series_Ema200_IsAllNullWhenFewerThan20Sessions()
    {
        Setup(Trending(10, adjusted: true));

        var s = await _sut.GetSeriesAsync("FPT");

        s.Ema200.Should().HaveCount(10).And.OnlyContain(v => v == null);
    }

    [Fact]
    public async Task Series_AtrUsesCandleRange_NotOnlyCloses()
    {
        // Đóng cửa phẳng: tính chỉ từ giá đóng cửa thì ATR = 0. Râu nến ±2.000 cho True Range = 4.000.
        Setup(Bars(60, adjusted: true));

        var s = await _sut.GetSeriesAsync("FPT");

        s.Atr14.Last().Should().Be(4_000m);
    }

    [Fact]
    public async Task Series_StochasticUsesHighLow_NotCloseRange()
    {
        // Đóng cửa phẳng + râu nến: %K tính theo cao/thấp = (100.000−98.000)/4.000 = 50
        Setup(Bars(60, adjusted: true));

        var s = await _sut.GetSeriesAsync("FPT");

        s.StochK.Last().Should().Be(50m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Analyze_CarriesAdjustedFlagFromBars(bool adjusted)
    {
        Setup(Trending(60, adjusted));

        (await _sut.AnalyzeAsync("FPT")).Adjusted.Should().Be(adjusted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Series_CarriesAdjustedFlagFromBars(bool adjusted)
    {
        Setup(Trending(60, adjusted));

        (await _sut.GetSeriesAsync("FPT")).Adjusted.Should().Be(adjusted);
    }

    [Fact]
    public async Task Analyze_TooFewBars_StillCarriesFlag()
    {
        Setup(Trending(5, adjusted: true));

        (await _sut.AnalyzeAsync("FPT")).Adjusted.Should().BeTrue();
    }

    [Fact]
    public async Task NoBars_AdjustedIsNull_NotFalse()
    {
        Setup(new List<StockPriceData>());

        (await _sut.AnalyzeAsync("FPT")).Adjusted.Should().BeNull("không có dữ liệu thì không biết, đừng nói là chưa điều chỉnh");
        (await _sut.GetSeriesAsync("FPT")).Adjusted.Should().BeNull();
    }
}
