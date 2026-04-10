using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceBollingerAtrTests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceBollingerAtrTests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── Bollinger Bands Tests ───────────────────────────────────────────

    [Fact]
    public async Task Analyze_BollingerMiddle_EqualsSma20()
    {
        // Arrange: 60 days of steady uptrend
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert: middle band should be SMA(20)
        result.BollingerMiddle.Should().NotBeNull();
        var last20 = prices.TakeLast(20).Select(p => p.Close).ToList();
        var expectedSma = Math.Round(last20.Average(), 0);
        result.BollingerMiddle.Should().Be(expectedSma);
    }

    [Fact]
    public async Task Analyze_BollingerBands_UpperAndLowerCorrect()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.BollingerUpper.Should().NotBeNull();
        result.BollingerLower.Should().NotBeNull();
        result.BollingerUpper.Should().BeGreaterThan(result.BollingerMiddle!.Value);
        result.BollingerLower.Should().BeLessThan(result.BollingerMiddle!.Value);

        // Verify: upper = middle + 2*stddev, lower = middle - 2*stddev
        var last20 = prices.TakeLast(20).Select(p => p.Close).ToList();
        var mean = last20.Average();
        var variance = last20.Average(p => (p - mean) * (p - mean));
        var stddev = (decimal)Math.Sqrt((double)variance);
        var expectedUpper = Math.Round(mean + 2 * stddev, 0);
        var expectedLower = Math.Round(mean - 2 * stddev, 0);

        result.BollingerUpper.Should().Be(expectedUpper);
        result.BollingerLower.Should().Be(expectedLower);
    }

    [Fact]
    public async Task Analyze_BollingerBandwidth_IsPositive()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.BollingerBandwidth.Should().NotBeNull();
        result.BollingerBandwidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_BollingerPercentB_BetweenMinus1And2()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.BollingerPercentB.Should().NotBeNull();
        // %B is usually between -0.5 and 1.5 for normal conditions
        result.BollingerPercentB.Should().BeInRange(-1m, 2m);
    }

    [Fact]
    public async Task Analyze_BollingerSqueeze_WhenLowBandwidth()
    {
        // Generate flat prices to create squeeze
        var prices = GenerateFlatPrices(60, 50_000m, 50m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.BollingerSignal.Should().NotBeNull();
        // Very flat prices → squeeze
        result.BollingerSignal.Should().Be("squeeze");
    }

    [Fact]
    public async Task Analyze_InsufficientData_BollingerNull()
    {
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // Less than 20 data points → bollinger null
        result.BollingerMiddle.Should().BeNull();
        result.BollingerUpper.Should().BeNull();
        result.BollingerLower.Should().BeNull();
    }

    // ─── ATR Tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Atr14_IsPositive()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Atr14.Should().NotBeNull();
        result.Atr14.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_Atr14_CalculatesCorrectly()
    {
        // Generate prices with known High/Low spread
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        // Each day: High = Close + 1000, Low = Close - 1000, so True Range ≈ 2000
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Atr14.Should().NotBeNull();
        // True range should be around 2000-2200 (High-Low=2000, plus daily change)
        result.Atr14.Should().BeGreaterThan(1800);
        result.Atr14.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task Analyze_AtrPercent_IsPercentOfPrice()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.AtrPercent.Should().NotBeNull();
        result.AtrPercent.Should().BeGreaterThan(0);
        // ATR% = ATR / CurrentPrice * 100, should be small %
        result.AtrPercent.Should().BeLessThan(20);
    }

    [Fact]
    public async Task Analyze_InsufficientData_AtrNull()
    {
        var prices = GeneratePriceHistory(10, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Atr14.Should().BeNull();
        result.AtrPercent.Should().BeNull();
    }

    // ─── Signal Scoring Integration ──────────────────────────────────────

    [Fact]
    public async Task Analyze_SignalCount_Includes10Indicators()
    {
        var prices = GeneratePriceHistory(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // 10 votes: EMA, RSI, MACD, Volume, Bollinger, ATR, Stochastic, ADX, OBV, MFI
        (result.BullishCount + result.BearishCount + result.NeutralCount).Should().Be(10);
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

    private static List<StockPriceData> GenerateFlatPrices(
        int days, decimal basePrice, decimal noise, long baseVolume = 1_000_000)
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-days);
        var rng = new Random(42);

        for (int i = 0; i < days; i++)
        {
            var jitter = (decimal)(rng.NextDouble() - 0.5) * noise;
            var close = basePrice + jitter;
            prices.Add(new StockPriceData
            {
                Symbol = "VNM",
                Date = baseDate.AddDays(i),
                Open = close - 10m,
                High = close + 20m,
                Low = close - 20m,
                Close = close,
                Volume = baseVolume
            });
        }

        return prices;
    }
}
