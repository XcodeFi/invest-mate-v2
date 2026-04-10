using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceTests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    private const string Symbol = "VNM";

    public TechnicalIndicatorServiceTests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── Helper methods ─────────────────────────────────────────────────

    private static List<StockPriceData> GeneratePriceHistory(
        int days,
        decimal startPrice = 80_000m,
        decimal dailyChange = 100m,
        long baseVolume = 1_000_000)
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-days);

        for (int i = 0; i < days; i++)
        {
            var close = startPrice + (i * dailyChange);
            prices.Add(new StockPriceData
            {
                Symbol = Symbol,
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

    /// <summary>
    /// Generate a price series that oscillates, creating swing highs and swing lows for support/resistance.
    /// </summary>
    private static List<StockPriceData> GenerateOscillatingPrices(
        int days,
        decimal basePrice = 50_000m,
        decimal amplitude = 5_000m,
        int period = 10,
        long baseVolume = 1_000_000)
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-days);

        for (int i = 0; i < days; i++)
        {
            var sineValue = (decimal)Math.Sin(2 * Math.PI * i / period);
            var close = basePrice + (amplitude * sineValue);
            prices.Add(new StockPriceData
            {
                Symbol = Symbol,
                Date = baseDate.AddDays(i),
                Open = close - 200m,
                High = close + 500m,
                Low = close - 500m,
                Close = close,
                Volume = baseVolume
            });
        }

        return prices;
    }

    private void SetupHistory(List<StockPriceData> prices)
    {
        _marketDataMock
            .Setup(m => m.GetHistoricalPricesAsync(Symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);
    }

    // ─── Tests: Insufficient data ────────────────────────────────────────

    [Fact]
    public async Task Analyze_EmptyHistory_ReturnsDefaultHoldSignal()
    {
        // Arrange
        SetupHistory(new List<StockPriceData>());

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.Symbol.Should().Be(Symbol);
        result.DataPoints.Should().Be(0);
        result.OverallSignal.Should().Be("hold");
        result.OverallSignalVi.Should().Contain("dữ liệu");
    }

    [Fact]
    public async Task Analyze_LessThan20DataPoints_ReturnsInsufficientDataSignal()
    {
        // Arrange — 15 data points (< 20 threshold)
        var prices = GeneratePriceHistory(15);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.DataPoints.Should().Be(15);
        result.OverallSignal.Should().Be("hold");
        result.OverallSignalVi.Should().Be("Không đủ dữ liệu");
        // No indicator values should be computed
        result.Ema20.Should().BeNull();
        result.Rsi14.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_Exactly19DataPoints_ReturnsInsufficientDataSignal()
    {
        // Arrange — boundary: 19 < 20
        var prices = GeneratePriceHistory(19);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.DataPoints.Should().Be(19);
        result.OverallSignal.Should().Be("hold");
    }

    // ─── Tests: EMA calculation ──────────────────────────────────────────

    [Fact]
    public async Task Analyze_Exactly20DataPoints_CalculatesEma20()
    {
        // Arrange — exactly 20 data points, enough for EMA(20)
        var prices = GeneratePriceHistory(20, startPrice: 80_000m, dailyChange: 100m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.DataPoints.Should().Be(20);
        result.Ema20.Should().NotBeNull();
        // EMA50 requires 50 data points, so it should be null
        result.Ema50.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_SufficientData_CalculatesEma20AndEma50()
    {
        // Arrange — 100 data points, enough for EMA(20) and EMA(50)
        var prices = GeneratePriceHistory(100, startPrice: 80_000m, dailyChange: 100m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.Ema20.Should().NotBeNull();
        result.Ema50.Should().NotBeNull();
        // In a trending up market, EMA20 > EMA50 → bullish
        result.EmaTrend.Should().Be("bullish");
    }

    [Fact]
    public async Task Analyze_DowntrendingData_EmaTrendIsBearish()
    {
        // Arrange — prices decreasing over time
        var prices = GeneratePriceHistory(100, startPrice: 100_000m, dailyChange: -200m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.Ema20.Should().NotBeNull();
        result.Ema50.Should().NotBeNull();
        // In a downtrend, EMA20 < EMA50 → bearish
        result.EmaTrend.Should().Be("bearish");
    }

    // ─── Tests: RSI calculation ──────────────────────────────────────────

    [Fact]
    public async Task Analyze_StrongUptrend_RsiIsOverbought()
    {
        // Arrange — prices increasing sharply (all gains, no losses)
        var prices = GeneratePriceHistory(30, startPrice: 50_000m, dailyChange: 2_000m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.Rsi14.Should().NotBeNull();
        result.Rsi14!.Value.Should().BeGreaterThanOrEqualTo(70m);
        result.RsiSignal.Should().Be("overbought");
    }

    [Fact]
    public async Task Analyze_StrongDowntrend_RsiIsOversold()
    {
        // Arrange — prices decreasing sharply (all losses)
        var prices = GeneratePriceHistory(30, startPrice: 100_000m, dailyChange: -2_000m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.Rsi14.Should().NotBeNull();
        result.Rsi14!.Value.Should().BeLessThanOrEqualTo(30m);
        result.RsiSignal.Should().Be("oversold");
    }

    // ─── Tests: MACD calculation ─────────────────────────────────────────

    [Fact]
    public async Task Analyze_SufficientData_CalculatesMacd()
    {
        // Arrange — 100 data points, enough for MACD(12,26,9)
        // Need at least slowPeriod(26) + signalPeriod(9) = 35 points
        var prices = GeneratePriceHistory(100, startPrice: 80_000m, dailyChange: 200m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.MacdLine.Should().NotBeNull();
        result.SignalLine.Should().NotBeNull();
        result.MacdHistogram.Should().NotBeNull();
    }

    [Fact]
    public async Task Analyze_InsufficientDataForMacd_MacdIsNull()
    {
        // Arrange — 25 data points, not enough for MACD (needs slowPeriod(26) + signalPeriod(9) = 35)
        var prices = GeneratePriceHistory(25, startPrice: 80_000m, dailyChange: 200m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.MacdLine.Should().BeNull();
        result.SignalLine.Should().BeNull();
        result.MacdHistogram.Should().BeNull();
    }

    // ─── Tests: Volume analysis ──────────────────────────────────────────

    [Fact]
    public async Task Analyze_NormalVolume_VolumeSignalIsNormal()
    {
        // Arrange — consistent volume across all days
        var prices = GeneratePriceHistory(30, startPrice: 80_000m, dailyChange: 100m, baseVolume: 1_000_000);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.AvgVolume20.Should().NotBeNull();
        result.VolumeRatio.Should().NotBeNull();
        // Volume is slowly increasing (baseVolume + i*10_000), so ratio should be close to 1
        result.VolumeSignal.Should().BeOneOf("normal", "high");
    }

    [Fact]
    public async Task Analyze_SpikeVolume_VolumeSignalIsSpike()
    {
        // Arrange — consistent volume but last day has a huge spike
        var prices = GeneratePriceHistory(30, startPrice: 80_000m, dailyChange: 100m, baseVolume: 1_000_000);
        // Set last day volume to 3x the average
        prices[^1] = new StockPriceData
        {
            Symbol = Symbol,
            Date = prices[^1].Date,
            Open = prices[^1].Open,
            High = prices[^1].High,
            Low = prices[^1].Low,
            Close = prices[^1].Close,
            Volume = 3_000_000
        };
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.VolumeSignal.Should().Be("spike");
        result.VolumeRatio!.Value.Should().BeGreaterThanOrEqualTo(2.0m);
    }

    // ─── Tests: Price change ─────────────────────────────────────────────

    [Fact]
    public async Task Analyze_PriceChange_CalculatesCorrectly()
    {
        // Arrange
        var prices = GeneratePriceHistory(25, startPrice: 80_000m, dailyChange: 500m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        var lastClose = prices[^1].Close;
        var prevClose = prices[^2].Close;
        result.CurrentPrice.Should().Be(lastClose);
        result.PriceChange.Should().Be(lastClose - prevClose);
        result.PriceChangePercent.Should().BeApproximately((lastClose - prevClose) / prevClose * 100m, 0.01m);
    }

    // ─── Tests: Overall signal logic ─────────────────────────────────────

    [Fact]
    public async Task Analyze_StrongUptrend_EmaBullishAndRsiOverbought()
    {
        // In a strong monotonic uptrend:
        // - EMA20 > EMA50 → bullish
        // - RSI → overbought (bearish, because it's been going up relentlessly)
        // - Volume spike with positive change → bullish
        // This combination means bullish >= 2 but bearish >= 1 too, so
        // overall signal depends on exact counts. We verify the individual indicators.
        var prices = GeneratePriceHistory(120, startPrice: 50_000m, dailyChange: 500m);
        // Spike volume on last day with positive price change
        prices[^1] = new StockPriceData
        {
            Symbol = Symbol,
            Date = prices[^1].Date,
            Open = prices[^1].Close - 1000m,
            High = prices[^1].Close + 2000m,
            Low = prices[^1].Close - 2000m,
            Close = prices[^1].Close,
            Volume = 5_000_000
        };
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert — verify individual indicator signals are as expected for strong uptrend
        result.EmaTrend.Should().Be("bullish");
        result.RsiSignal.Should().Be("overbought"); // Strong uptrend → RSI high
        result.VolumeSignal.Should().Be("spike");
        // BullishCount should include EMA + Volume spike (positive price change)
        result.BullishCount.Should().BeGreaterThanOrEqualTo(2);
        // BearishCount should include RSI overbought
        result.BearishCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Analyze_AllBearishIndicators_ReturnsSellSignal()
    {
        // Arrange — strong downtrend with high volume spike on negative price day
        var prices = GeneratePriceHistory(120, startPrice: 150_000m, dailyChange: -500m);
        // Spike volume on last day (negative price change is implicit from dailyChange)
        prices[^1] = new StockPriceData
        {
            Symbol = Symbol,
            Date = prices[^1].Date,
            Open = prices[^1].Close + 1000m,
            High = prices[^1].Close + 2000m,
            Low = prices[^1].Close - 2000m,
            Close = prices[^1].Close,
            Volume = 5_000_000
        };
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        result.BearishCount.Should().BeGreaterThanOrEqualTo(2);
        result.OverallSignal.Should().BeOneOf("strong_sell", "sell");
    }

    // ─── Tests: Support / Resistance ─────────────────────────────────────

    [Fact]
    public async Task Analyze_OscillatingPrices_FindsSupportAndResistanceLevels()
    {
        // Arrange — oscillating prices create clear swing highs and swing lows
        var prices = GenerateOscillatingPrices(120, basePrice: 50_000m, amplitude: 5_000m, period: 20);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert
        // The oscillating data should produce swing highs/lows
        // Support levels should be below current price, resistance above
        result.SupportLevels.Should().NotBeNull();
        result.ResistanceLevels.Should().NotBeNull();
        // At least one support or resistance should be detected in 120 oscillating data points
        (result.SupportLevels.Count + result.ResistanceLevels.Count).Should().BeGreaterThan(0);
    }

    // ─── Tests: Trade suggestion ─────────────────────────────────────────

    [Fact]
    public async Task Analyze_WithSupportAndResistance_ProvidesTradeSuggestion()
    {
        // Arrange — oscillating prices for clear support/resistance
        var prices = GenerateOscillatingPrices(120, basePrice: 50_000m, amplitude: 5_000m, period: 20);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert — if both support and resistance found, trade suggestion should be present
        if (result.SupportLevels.Count > 0 && result.ResistanceLevels.Count > 0)
        {
            result.SuggestedEntry.Should().NotBeNull();
            result.SuggestedStopLoss.Should().NotBeNull();
            result.SuggestedTarget.Should().NotBeNull();
            result.SuggestedEntry!.Value.Should().BeLessThan(result.SuggestedTarget!.Value);
            result.SuggestedStopLoss!.Value.Should().BeLessThan(result.SuggestedEntry!.Value);
        }
    }

    // ─── Tests: Result structure ─────────────────────────────────────────

    [Fact]
    public async Task Analyze_ValidData_PopulatesAllResultFields()
    {
        // Arrange — 100 data points should be enough for all indicators
        var prices = GeneratePriceHistory(100, startPrice: 80_000m, dailyChange: 200m);
        SetupHistory(prices);

        // Act
        var result = await _sut.AnalyzeAsync(Symbol);

        // Assert — structural checks
        result.Symbol.Should().Be(Symbol);
        result.DataPoints.Should().Be(100);
        result.AnalyzedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.CurrentPrice.Should().Be(prices.Last().Close);
        result.CurrentVolume.Should().Be(prices.Last().Volume);

        // Signal counts should add up to 6 (EMA, RSI, MACD, Volume, Bollinger, ATR)
        (result.BullishCount + result.BearishCount + result.NeutralCount).Should().Be(6);

        // Overall signal should be one of the valid values
        result.OverallSignal.Should().BeOneOf("strong_buy", "buy", "hold", "sell", "strong_sell");
        result.OverallSignalVi.Should().BeOneOf("Mua mạnh", "Mua", "Chờ", "Bán", "Bán mạnh");
    }

    // ─── Tests: Configurable months parameter ───────────────────────────

    [Fact]
    public async Task AnalyzeAsync_WithMonthsParameter_FetchesCorrectDateRange()
    {
        // Arrange — request 12 months of data
        var prices = GeneratePriceHistory(60, startPrice: 80_000m, dailyChange: 200m);
        DateTime capturedFrom = default;
        _marketDataMock
            .Setup(m => m.GetHistoricalPricesAsync(Symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, DateTime, CancellationToken>((_, from, _, _) => capturedFrom = from)
            .ReturnsAsync(prices);

        // Act
        await _sut.AnalyzeAsync(Symbol, months: 12);

        // Assert — the 'from' date should be ~12 months ago (within a small tolerance)
        var expected = DateTime.UtcNow.AddMonths(-12);
        capturedFrom.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutMonthsParameter_DefaultsTo12Months()
    {
        // Arrange
        var prices = GeneratePriceHistory(60, startPrice: 80_000m, dailyChange: 200m);
        DateTime capturedFrom = default;
        _marketDataMock
            .Setup(m => m.GetHistoricalPricesAsync(Symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, DateTime, CancellationToken>((_, from, _, _) => capturedFrom = from)
            .ReturnsAsync(prices);

        // Act — no months parameter, should default to 12
        await _sut.AnalyzeAsync(Symbol);

        // Assert — the 'from' date should be ~12 months ago (not 6)
        var expected12MonthsAgo = DateTime.UtcNow.AddMonths(-12);
        var wrong6MonthsAgo = DateTime.UtcNow.AddMonths(-6);
        capturedFrom.Should().BeCloseTo(expected12MonthsAgo, TimeSpan.FromSeconds(5));
        capturedFrom.Should().NotBeCloseTo(wrong6MonthsAgo, TimeSpan.FromMinutes(1));
    }
}
