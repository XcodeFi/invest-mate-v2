using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceStopLossFieldsTests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceStopLossFieldsTests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── EMA(21) Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Ema21_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Ema21.Should().NotBeNull();
        result.Ema21.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_Ema21_BetweenEma20AndEma50()
    {
        // EMA21 should sit between EMA20 and EMA50 in a smooth trend
        var prices = GeneratePriceHistory(120, 30_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Ema21.Should().NotBeNull();
        result.Ema20.Should().NotBeNull();
        result.Ema50.Should().NotBeNull();

        // In uptrend: EMA20 > EMA21 > EMA50 (shorter EMA reacts faster)
        result.Ema21.Should().BeLessThan(result.Ema20!.Value);
        result.Ema21.Should().BeGreaterThan(result.Ema50!.Value);
    }

    [Fact]
    public async Task Analyze_Ema21_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Ema21.Should().BeNull();
    }

    // ─── HighestHigh22 / LowestLow22 Tests ──────────────────────────────

    [Fact]
    public async Task Analyze_HighestHigh22_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.HighestHigh22.Should().NotBeNull();
        result.HighestHigh22.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_LowestLow22_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.LowestLow22.Should().NotBeNull();
        result.LowestLow22.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_HighestHigh22_GreaterThanCurrentPrice_InDowntrend()
    {
        // In downtrend, highest high of last 22 bars should be above current close
        var prices = GeneratePriceHistory(60, 80_000m, -300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.HighestHigh22.Should().NotBeNull();
        result.HighestHigh22.Should().BeGreaterThan(result.CurrentPrice);
    }

    [Fact]
    public async Task Analyze_LowestLow22_LessThanCurrentPrice_InUptrend()
    {
        // In uptrend, lowest low of last 22 bars should be below current close
        var prices = GeneratePriceHistory(60, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.LowestLow22.Should().NotBeNull();
        result.LowestLow22.Should().BeLessThan(result.CurrentPrice);
    }

    [Fact]
    public async Task Analyze_HighestHigh22_GreaterThanOrEqualLowestLow22()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.HighestHigh22.Should().BeGreaterThanOrEqualTo(result.LowestLow22!.Value);
    }

    [Fact]
    public async Task Analyze_HighestHigh22_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.HighestHigh22.Should().BeNull();
        result.LowestLow22.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void SetupPrices(List<StockPriceData> prices)
    {
        _marketDataMock.Setup(m => m.GetHistoricalPricesAsync(
                "VNM", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);
    }

    private static List<StockPriceData> GeneratePriceHistory(
        int days, decimal startPrice, decimal dailyChange, long baseVolume = 1_000_000)
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-days);

        for (int i = 0; i < days; i++)
        {
            var close = startPrice + (i * dailyChange);
            prices.Add(new StockPriceData
            {
                Symbol = "VNM",
                Date = baseDate.AddDays(i),
                Open = close - 500m,
                High = close + 1000m,
                Low = close - 1000m,
                Close = close,
                Volume = baseVolume + (i * 10_000)
            });
        }

        return prices;
    }
}
