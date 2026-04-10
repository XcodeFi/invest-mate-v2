using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceFibonacciEma200Tests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceFibonacciEma200Tests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── Fibonacci Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_OscillatingPrices_FibonacciRetracementLevelsAreCorrect()
    {
        // Arrange — create prices with known swing low (~62,000) and swing high (~86,500)
        // Use oscillating prices where we can identify clear swing points
        var prices = GenerateOscillatingPricesWithKnownSwings(
            days: 60, swingLow: 62_000m, swingHigh: 86_500m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert
        result.Fibonacci.Should().NotBeNull();
        result.Fibonacci!.SwingHigh.Should().BeGreaterThan(result.Fibonacci.SwingLow);

        // Retracement levels: swingLow + (swingHigh - swingLow) * ratio
        var range = result.Fibonacci.SwingHigh - result.Fibonacci.SwingLow;

        // 23.6% retracement = swingLow + range * 0.236
        result.Fibonacci.Retracement236.Should().BeApproximately(
            result.Fibonacci.SwingLow + range * 0.236m, range * 0.01m);

        // 38.2% retracement = swingLow + range * 0.382
        result.Fibonacci.Retracement382.Should().BeApproximately(
            result.Fibonacci.SwingLow + range * 0.382m, range * 0.01m);

        // 50% retracement = swingLow + range * 0.5
        result.Fibonacci.Retracement500.Should().BeApproximately(
            result.Fibonacci.SwingLow + range * 0.5m, range * 0.01m);

        // 61.8% retracement = swingLow + range * 0.618
        result.Fibonacci.Retracement618.Should().BeApproximately(
            result.Fibonacci.SwingLow + range * 0.618m, range * 0.01m);

        // 78.6% retracement = swingLow + range * 0.786
        result.Fibonacci.Retracement786.Should().BeApproximately(
            result.Fibonacci.SwingLow + range * 0.786m, range * 0.01m);
    }

    [Fact]
    public async Task Analyze_OscillatingPrices_FibonacciExtensionLevelsAreCorrect()
    {
        // Arrange
        var prices = GenerateOscillatingPricesWithKnownSwings(
            days: 60, swingLow: 62_000m, swingHigh: 86_500m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert
        result.Fibonacci.Should().NotBeNull();
        var range = result.Fibonacci!.SwingHigh - result.Fibonacci.SwingLow;

        // 127.2% extension = swingHigh + range * 0.272
        result.Fibonacci.Extension1272.Should().BeApproximately(
            result.Fibonacci.SwingHigh + range * 0.272m, range * 0.01m);

        // 161.8% extension = swingHigh + range * 0.618
        result.Fibonacci.Extension1618.Should().BeApproximately(
            result.Fibonacci.SwingHigh + range * 0.618m, range * 0.01m);
    }

    [Fact]
    public async Task Analyze_InsufficientData_FibonacciIsNull()
    {
        // Arrange — less than 20 data points → insufficient for swing detection
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert — insufficient data returns hold signal, no Fibonacci
        result.Fibonacci.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_FlatPrices_FibonacciIsNull()
    {
        // Arrange — flat prices with no swing highs/lows
        var prices = GenerateFlatPrices(60, 50_000m, 10m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert — no clear swings → Fibonacci null
        result.Fibonacci.Should().BeNull();
    }

    // ─── EMA200 Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_SufficientData_Ema200IsCalculated()
    {
        // Arrange — 220 data points, enough for EMA(200)
        var prices = GeneratePriceHistory(220, 50_000m, 100m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert
        result.Ema200.Should().NotBeNull();
        result.Ema200.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_InsufficientDataForEma200_Ema200IsNull()
    {
        // Arrange — 100 data points, not enough for EMA(200)
        var prices = GeneratePriceHistory(100, 50_000m, 100m);
        SetupPrices(prices);

        // Act
        var result = await _sut.AnalyzeAsync("VNM");

        // Assert
        result.Ema200.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void SetupPrices(List<StockPriceData> prices)
    {
        _marketDataMock.Setup(m => m.GetHistoricalPricesAsync(
                "VNM", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);
    }

    /// <summary>
    /// Generate prices that oscillate between known swing low and swing high,
    /// creating clear swing points for Fibonacci calculation.
    /// Pattern: start at midpoint, go down to swingLow, up to swingHigh, then back to midpoint area.
    /// </summary>
    private static List<StockPriceData> GenerateOscillatingPricesWithKnownSwings(
        int days, decimal swingLow, decimal swingHigh)
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-days);
        var midpoint = (swingLow + swingHigh) / 2;
        var amplitude = (swingHigh - swingLow) / 2;

        for (int i = 0; i < days; i++)
        {
            // Create a sine wave that hits swingLow and swingHigh
            var sineValue = (decimal)Math.Sin(2 * Math.PI * i / 20);
            var close = midpoint + amplitude * sineValue;
            prices.Add(new StockPriceData
            {
                Symbol = "VNM",
                Date = baseDate.AddDays(i),
                Open = close - 200m,
                High = close + 500m,
                Low = close - 500m,
                Close = close,
                Volume = 1_000_000
            });
        }

        return prices;
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
