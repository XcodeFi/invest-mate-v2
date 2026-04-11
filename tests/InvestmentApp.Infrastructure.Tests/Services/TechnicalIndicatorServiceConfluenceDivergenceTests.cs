using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceConfluenceDivergenceTests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceConfluenceDivergenceTests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── Confluence Score Tests ─────────────────────────────────────────

    [Fact]
    public async Task Analyze_ConfluenceScore_CalculatesValue()
    {
        var prices = GeneratePriceHistory(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.ConfluenceScore.Should().NotBeNull();
        result.ConfluenceScore.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_ConfluenceScore_StrongUptrend_HighScore()
    {
        // All indicators bullish → score should be > 60
        var prices = GeneratePriceHistory(120, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.ConfluenceScore.Should().NotBeNull();
        result.ConfluenceScore.Should().BeGreaterThan(60);
    }

    [Fact]
    public async Task Analyze_ConfluenceScore_StrongDowntrend_LowScore()
    {
        // All indicators bearish → score should be < 40
        var prices = GeneratePriceHistory(120, 100_000m, -300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.ConfluenceScore.Should().NotBeNull();
        result.ConfluenceScore.Should().BeLessThan(40);
    }

    [Fact]
    public async Task Analyze_ConfluenceScore_FlatMarket_MidRange()
    {
        // Mixed signals → score should be in 30-70 range
        var prices = GenerateFlatPrices(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.ConfluenceScore.Should().NotBeNull();
        result.ConfluenceScore.Should().BeInRange(30, 70);
    }

    [Fact]
    public async Task Analyze_ConfluenceScore_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(10, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.ConfluenceScore.Should().BeNull();
    }

    // ─── Market Condition Classifier Tests ───────────────────────────────

    [Fact]
    public async Task Analyze_MarketCondition_StrongTrend()
    {
        // Strong consistent trend → ADX ≥ 40 → "strong_trend"
        var prices = GeneratePriceHistory(120, 30_000m, 500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.MarketCondition.Should().Be("strong_trend");
        result.SuggestedStrategy.Should().Contain("Trend Following");
    }

    [Fact]
    public async Task Analyze_MarketCondition_Trending()
    {
        // Moderate trend → ADX 25-40 → "trending"
        var prices = GeneratePriceHistory(120, 30_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.MarketCondition.Should().BeOneOf("trending", "strong_trend");
    }

    [Fact]
    public async Task Analyze_MarketCondition_FlatPrices_Sideway()
    {
        // Flat prices → ADX < 20 → "sideway", suggest Mean Reversion
        var prices = GenerateFlatPrices(120, 50_000m, 100m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.MarketCondition.Should().Be("sideway");
        result.SuggestedStrategy.Should().Contain("Mean Reversion");
    }

    [Fact]
    public async Task Analyze_MarketCondition_HasVietnameseLabel()
    {
        var prices = GeneratePriceHistory(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.MarketConditionVi.Should().NotBeNullOrEmpty();
        result.MarketConditionVi.Should().NotBe("Chưa xác định");
    }

    [Fact]
    public async Task Analyze_MarketCondition_InsufficientData_Unknown()
    {
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // ADX is null with < 29 candles → market condition stays "unknown"
        result.MarketCondition.Should().Be("unknown");
    }

    // ─── Divergence Detection Tests ─────────────────────────────────────

    [Fact]
    public async Task Analyze_Divergence_BullishPattern()
    {
        // Pattern: price makes lower low, RSI makes higher low → bullish divergence
        var prices = GenerateBullishDivergencePattern();
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.RsiDivergence.Should().Be("bullish");
        result.DivergenceSignal.Should().Be("bullish_divergence");
    }

    [Fact]
    public async Task Analyze_Divergence_BearishPattern()
    {
        // Pattern: price makes higher high, RSI makes lower high → bearish divergence
        var prices = GenerateBearishDivergencePattern();
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.RsiDivergence.Should().Be("bearish");
        result.DivergenceSignal.Should().Be("bearish_divergence");
    }

    [Fact]
    public async Task Analyze_Divergence_NormalTrend_NoDivergence()
    {
        // Consistent uptrend: price up, RSI up → no divergence
        var prices = GeneratePriceHistory(120, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.RsiDivergence.Should().BeNull();
        result.DivergenceSignal.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_Divergence_HasVietnameseSignal()
    {
        var prices = GenerateBullishDivergencePattern();
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.DivergenceSignalVi.Should().NotBeNull();
        result.DivergenceSignalVi.Should().Contain("Phân kỳ");
    }

    [Fact]
    public async Task Analyze_Divergence_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.RsiDivergence.Should().BeNull();
        result.MacdDivergence.Should().BeNull();
        result.DivergenceSignal.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_Divergence_FlatMarket_NoDivergence()
    {
        // Flat market without meaningful swings → no divergence
        var prices = GenerateFlatPrices(120, 50_000m, 100m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.DivergenceSignal.Should().BeNull();
    }

    // ─── Integration: All 3 features together ───────────────────────────

    [Fact]
    public async Task Analyze_AllP2Features_PopulatedTogether()
    {
        var prices = GeneratePriceHistory(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // Confluence score populated
        result.ConfluenceScore.Should().NotBeNull();
        // Market condition populated
        result.MarketCondition.Should().NotBe("unknown");
        result.MarketConditionVi.Should().NotBe("Chưa xác định");
        result.SuggestedStrategy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Analyze_ConfluenceScore_ConsistentWithVoting()
    {
        // High confluence score should correlate with bullish voting
        var prices = GeneratePriceHistory(120, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // When bullish count is high, confluence should be high too
        if (result.BullishCount >= 6)
        {
            result.ConfluenceScore.Should().BeGreaterThan(55);
        }
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

    /// <summary>
    /// Bullish divergence: price makes lower low, but RSI makes higher low.
    /// Phase 0: Stable (30 days)
    /// Phase 1: Sharp decline -1000/day for 12 days → deep RSI (bottom ~38,000)
    /// Phase 2: Rally +500/day for 12 days → RSI recovers
    /// Phase 3: Gentle decline -400/day for 18 days → lower price with less momentum
    /// Phase 4: Small recovery +300/day for 12 days → creates detectable V-bottom
    /// </summary>
    private static List<StockPriceData> GenerateBullishDivergencePattern()
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-84);

        // Phase 0: Stable (30 days at ~50,000)
        for (int i = 0; i < 30; i++)
        {
            AddPrice(prices, baseDate, i, 50_000m + ((i % 3) - 1) * 100m);
        }

        // Phase 1: Sharp decline (12 days, -1000/day → bottom at 38,000)
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 30 + i, 50_000m - (i * 1000m));
        }

        // Phase 2: Rally (12 days, +500/day → up to 43,500)
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 42 + i, 38_000m + (i * 500m));
        }

        // Phase 3: Gentle decline (18 days, -400/day → bottom at 36,200)
        for (int i = 0; i < 18; i++)
        {
            AddPrice(prices, baseDate, 54 + i, 43_500m - (i * 400m));
        }
        // Bottom = 43,500 - 17*400 = 36,700 < 38,000 ✓ (lower low)
        // Daily change -400 vs -1000 → RSI should be higher ✓

        // Phase 4: Small recovery (12 days, +300/day → creates V-bottom for swing detection)
        var phase3Bottom = 43_500m - (17 * 400m); // 36,700
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 72 + i, phase3Bottom + (i * 300m));
        }

        return prices; // Total: 30 + 12 + 12 + 18 + 12 = 84 days
    }

    /// <summary>
    /// Bearish divergence: price makes higher high, but RSI makes lower high.
    /// Phase 0: Stable (30 days)
    /// Phase 1: Sharp rally +1000/day for 12 days → high RSI (peak ~62,000)
    /// Phase 2: Pullback -500/day for 12 days → RSI drops
    /// Phase 3: Gentle rally +400/day for 18 days → higher price, lower momentum
    /// Phase 4: Small pullback → creates detectable swing high
    /// </summary>
    private static List<StockPriceData> GenerateBearishDivergencePattern()
    {
        var prices = new List<StockPriceData>();
        var baseDate = DateTime.UtcNow.AddDays(-84);

        // Phase 0: Stable at ~50,000 (30 days)
        for (int i = 0; i < 30; i++)
        {
            AddPrice(prices, baseDate, i, 50_000m + ((i % 3) - 1) * 100m);
        }

        // Phase 1: Sharp rally (12 days, +1000/day → peaks at ~61,000)
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 30 + i, 50_000m + (i * 1000m));
        }

        // Phase 2: Pullback (12 days, -500/day → down to ~55,500)
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 42 + i, 61_000m - (i * 500m));
        }

        // Phase 3: Gentle rally (18 days, +400/day → peaks at ~62,300)
        for (int i = 0; i < 18; i++)
        {
            AddPrice(prices, baseDate, 54 + i, 55_500m + (i * 400m));
        }
        // Peak = 55,500 + 17*400 = 62,300 > 61,000 ✓ (higher high)
        // Daily change +400 vs +1000 → RSI should be lower ✓

        // Phase 4: Small pullback (12 days, -300/day → creates swing high for detection)
        var phase3Peak = 55_500m + (17 * 400m); // 62,300
        for (int i = 0; i < 12; i++)
        {
            AddPrice(prices, baseDate, 72 + i, phase3Peak - (i * 300m));
        }

        return prices; // Total: 30 + 12 + 12 + 18 + 12 = 84 days
    }

    private static void AddPrice(List<StockPriceData> prices, DateTime baseDate, int dayOffset, decimal close)
    {
        prices.Add(new StockPriceData
        {
            Symbol = "VNM",
            Date = baseDate.AddDays(dayOffset),
            Open = close - 300m,
            High = close + 500m,
            Low = close - 500m,
            Close = close,
            Volume = 1_000_000
        });
    }
}
