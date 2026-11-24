using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Chuỗi chỉ báo theo từng phiên (GetSeriesAsync) phải dùng CHUNG công thức với giá trị scalar của
/// AnalyzeAsync: phần tử cuối của mỗi chuỗi == scalar tương ứng, để đường vẽ khớp con số trên thẻ.
/// </summary>
public class TechnicalIndicatorServiceSeriesTests
{
    private const string Symbol = "FPT";

    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceSeriesTests()
    {
        _sut = new TechnicalIndicatorService(_marketData.Object);
    }

    /// <summary>Bộ giá cố định (seed cố định) có lên xuống thật để mọi chỉ báo có giá trị khác tầm thường.</summary>
    private static List<StockPriceData> Prices(int days, int seed = 7)
    {
        var rng = new Random(seed);
        var list = new List<StockPriceData>();
        var close = 80_000m;
        var date = DateTime.UtcNow.Date.AddDays(-days);
        for (int i = 0; i < days; i++)
        {
            var open = close;
            close = Math.Max(1_000m, close + rng.Next(-1_500, 1_600));
            var high = Math.Max(open, close) + rng.Next(0, 900);
            var low = Math.Min(open, close) - rng.Next(0, 900);
            list.Add(new StockPriceData
            {
                Symbol = Symbol, Date = date.AddDays(i),
                Open = open, High = high, Low = low, Close = close,
                Volume = 1_000_000 + rng.Next(0, 800_000)
            });
        }
        return list;
    }

    private void Setup(List<StockPriceData> prices) =>
        _marketData.Setup(m => m.GetHistoricalPricesAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

    private static IEnumerable<(string Name, List<decimal?> Series)> AllSeries(TechnicalSeriesResult s) => new[]
    {
        (nameof(s.Ema20), s.Ema20), (nameof(s.Ema50), s.Ema50),
        (nameof(s.BollingerUpper), s.BollingerUpper), (nameof(s.BollingerMiddle), s.BollingerMiddle),
        (nameof(s.BollingerLower), s.BollingerLower), (nameof(s.Rsi14), s.Rsi14),
        (nameof(s.StochK), s.StochK), (nameof(s.StochD), s.StochD),
        (nameof(s.MacdLine), s.MacdLine), (nameof(s.MacdSignal), s.MacdSignal), (nameof(s.MacdHistogram), s.MacdHistogram),
        (nameof(s.Adx), s.Adx), (nameof(s.PlusDi), s.PlusDi), (nameof(s.MinusDi), s.MinusDi),
        (nameof(s.Atr14), s.Atr14), (nameof(s.Obv), s.Obv), (nameof(s.Mfi14), s.Mfi14),
        (nameof(s.VolumeSma20), s.VolumeSma20)
    };

    [Theory]
    [InlineData(150, 7)]
    [InlineData(150, 99)]
    [InlineData(60, 3)]
    public async Task Series_LastPoint_Equals_Scalar_For_All_18_Indicators(int days, int seed)
    {
        Setup(Prices(days, seed));

        var scalar = await _sut.AnalyzeAsync(Symbol);
        var series = await _sut.GetSeriesAsync(Symbol);

        series.Ema20.Last().Should().Be(scalar.Ema20);
        series.Ema50.Last().Should().Be(scalar.Ema50);
        series.BollingerUpper.Last().Should().Be(scalar.BollingerUpper);
        series.BollingerMiddle.Last().Should().Be(scalar.BollingerMiddle);
        series.BollingerLower.Last().Should().Be(scalar.BollingerLower);
        series.Rsi14.Last().Should().Be(scalar.Rsi14);
        series.StochK.Last().Should().Be(scalar.StochasticK);
        series.StochD.Last().Should().Be(scalar.StochasticD);
        series.MacdLine.Last().Should().Be(scalar.MacdLine);
        series.MacdSignal.Last().Should().Be(scalar.SignalLine);
        series.MacdHistogram.Last().Should().Be(scalar.MacdHistogram);
        series.Adx.Last().Should().Be(scalar.Adx14);
        series.PlusDi.Last().Should().Be(scalar.PlusDi);
        series.MinusDi.Last().Should().Be(scalar.MinusDi);
        series.Atr14.Last().Should().Be(scalar.Atr14);
        series.Obv.Last().Should().Be(scalar.Obv);
        series.Mfi14.Last().Should().Be(scalar.Mfi14);
        series.VolumeSma20.Last().Should().Be(scalar.AvgVolume20);
    }

    [Fact]
    public async Task Series_Last_Values_Are_Not_All_Null_On_150_Bars()
    {
        // Chặn parity rỗng ruột: cùng null == null vẫn "khớp".
        Setup(Prices(150));

        var series = await _sut.GetSeriesAsync(Symbol);

        foreach (var (name, values) in AllSeries(series))
            values.Last().Should().NotBeNull($"{name} phải có giá trị ở phiên cuối khi có 150 phiên");
    }

    [Fact]
    public async Task Series_Arrays_Have_Same_Length_As_Dates()
    {
        var prices = Prices(150);
        Setup(prices);

        var series = await _sut.GetSeriesAsync(Symbol);

        series.Dates.Should().HaveCount(150);
        series.Open.Should().HaveCount(150);
        series.High.Should().HaveCount(150);
        series.Low.Should().HaveCount(150);
        series.Close.Should().HaveCount(150).And.Equal(prices.Select(p => p.Close));
        series.Volume.Should().HaveCount(150).And.Equal(prices.Select(p => p.Volume));
        foreach (var (name, values) in AllSeries(series))
            values.Should().HaveCount(150, $"{name} phải dài bằng Dates");
    }

    [Fact]
    public async Task Ema20_Is_Null_Before_Warmup_And_Set_At_Index_19()
    {
        Setup(Prices(150));

        var series = await _sut.GetSeriesAsync(Symbol);

        series.Ema20.Take(19).Should().OnlyContain(x => x == null);
        series.Ema20[19].Should().NotBeNull();
        series.Ema50.Take(49).Should().OnlyContain(x => x == null);
        series.Ema50[49].Should().NotBeNull();
    }

    [Fact]
    public async Task Under_20_Bars_Returns_Candles_And_All_Null_Indicators()
    {
        Setup(Prices(15));

        var series = await _sut.GetSeriesAsync(Symbol);

        series.Close.Should().HaveCount(15);
        series.Dates.Should().HaveCount(15);
        foreach (var (name, values) in AllSeries(series))
        {
            values.Should().HaveCount(15, name);
            values.Should().OnlyContain(x => x == null, name);
        }
    }

    [Fact]
    public async Task No_Data_Returns_Empty_Arrays_Without_Throwing()
    {
        Setup(new List<StockPriceData>());

        var series = await _sut.GetSeriesAsync(Symbol);

        series.Dates.Should().BeEmpty();
        series.Close.Should().BeEmpty();
        foreach (var (_, values) in AllSeries(series)) values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSeriesAsync_Uses_Same_Date_Range_As_AnalyzeAsync()
    {
        Setup(Prices(150));

        await _sut.GetSeriesAsync(Symbol);

        _marketData.Verify(m => m.GetHistoricalPricesAsync(
            Symbol,
            It.Is<DateTime>(from => Math.Abs((from - DateTime.UtcNow.AddMonths(-12)).TotalMinutes) < 5),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Series_Is_Stable_When_History_Grows_By_One_Bar()
    {
        // Mỗi điểm chỉ phụ thuộc quá khứ: thêm 1 phiên không đổi các điểm trước đó (trừ Obv/MFI giữ nguyên quy tắc).
        var prices = Prices(151);
        Setup(prices.Take(150).ToList());
        var before = await _sut.GetSeriesAsync(Symbol);
        Setup(prices);
        var after = await _sut.GetSeriesAsync(Symbol);

        foreach (var ((name, a), (_, b)) in AllSeries(before).Zip(AllSeries(after)))
            b.Take(150).Should().Equal(a, $"{name} không được đổi ở các phiên cũ");
    }
}
