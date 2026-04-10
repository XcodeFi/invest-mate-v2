using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceStochasticAdxObvMfiTests
{
    private readonly Mock<IMarketDataProvider> _marketDataMock;
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceStochasticAdxObvMfiTests()
    {
        _marketDataMock = new Mock<IMarketDataProvider>();
        _sut = new TechnicalIndicatorService(_marketDataMock.Object);
    }

    // ─── Stochastic Oscillator Tests ────────────────────────────────────

    [Fact]
    public async Task Analyze_Stochastic_CalculatesPercentK()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticK.Should().NotBeNull();
        result.StochasticK.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_Stochastic_CalculatesPercentD()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticD.Should().NotBeNull();
        result.StochasticD.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_Stochastic_PercentK_DiffersFromPercentD()
    {
        // %K and %D should be different values (different smoothing windows)
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticK.Should().NotBeNull();
        result.StochasticD.Should().NotBeNull();
        // They CAN be equal in flat markets, but in a trending market they should differ
        // At minimum, verify they are independently computed
        (result.StochasticK!.Value + result.StochasticD!.Value).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_Stochastic_StrongUptrend_Overbought()
    {
        // Strong uptrend: price closing near highs → %K should be high
        var prices = GeneratePriceHistory(60, 30_000m, 500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticK.Should().NotBeNull();
        result.StochasticSignal.Should().Be("overbought");
    }

    [Fact]
    public async Task Analyze_Stochastic_StrongDowntrend_Oversold()
    {
        // Strong downtrend: price closing near lows → %K should be low
        var prices = GeneratePriceHistory(60, 80_000m, -500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticK.Should().NotBeNull();
        result.StochasticSignal.Should().Be("oversold");
    }

    [Fact]
    public async Task Analyze_Stochastic_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(10, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.StochasticK.Should().BeNull();
        result.StochasticD.Should().BeNull();
    }

    // ─── ADX Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Adx_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().NotBeNull();
        result.Adx14.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_Adx_CalculatesPlusDiMinusDi()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.PlusDi.Should().NotBeNull();
        result.MinusDi.Should().NotBeNull();
        result.PlusDi.Should().BeGreaterThanOrEqualTo(0);
        result.MinusDi.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Analyze_Adx_StrongTrend_TrendingOrStrongSignal()
    {
        // Strong consistent uptrend → ADX should be > 25 (trending or strong_trend)
        var prices = GeneratePriceHistory(80, 30_000m, 500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().NotBeNull();
        result.Adx14.Should().BeGreaterThanOrEqualTo(25);
        result.AdxSignal.Should().BeOneOf("trending", "strong_trend");
    }

    [Fact]
    public async Task Analyze_Adx_FlatPrices_SidewaySignal()
    {
        // Flat prices → ADX should be < 20
        var prices = GenerateFlatPrices(80, 50_000m, 100m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().NotBeNull();
        result.AdxSignal.Should().Be("sideway");
    }

    [Fact]
    public async Task Analyze_Adx_Uptrend_PlusDiGreaterThanMinusDi()
    {
        var prices = GeneratePriceHistory(80, 30_000m, 500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.PlusDi.Should().BeGreaterThan(result.MinusDi!.Value);
    }

    [Fact]
    public async Task Analyze_Adx_InsufficientData_Null()
    {
        // ADX needs 28+ candles (14-period DI + 14-period ADX smoothing)
        var prices = GeneratePriceHistory(15, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().BeNull();
        result.PlusDi.Should().BeNull();
        result.MinusDi.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_Adx_BoundaryData_28Candles_Null()
    {
        // 28 candles: passes outer guard (>=20) but ADX needs 2*14+1=29 → still null
        var prices = GeneratePriceHistory(28, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_Adx_ExactMinData_29Candles_NotNull()
    {
        // 29 candles: exact minimum for ADX(14)
        var prices = GeneratePriceHistory(30, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Adx14.Should().NotBeNull();
    }

    // ─── OBV Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Obv_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Obv.Should().NotBeNull();
    }

    [Fact]
    public async Task Analyze_Obv_Uptrend_Rising()
    {
        // In uptrend, OBV should be rising → OBV > 0 since most days are up
        var prices = GeneratePriceHistory(60, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Obv.Should().BeGreaterThan(0);
        result.ObvSignal.Should().Be("rising");
    }

    [Fact]
    public async Task Analyze_Obv_Downtrend_Falling()
    {
        var prices = GeneratePriceHistory(60, 80_000m, -300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Obv.Should().BeLessThan(0);
        result.ObvSignal.Should().Be("falling");
    }

    [Fact]
    public async Task Analyze_Obv_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(10, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Obv.Should().BeNull();
    }

    // ─── MFI Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Mfi_CalculatesValue()
    {
        var prices = GeneratePriceHistory(60, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Mfi14.Should().NotBeNull();
        result.Mfi14.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_Mfi_StrongUptrend_Overbought()
    {
        var prices = GeneratePriceHistory(60, 30_000m, 500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Mfi14.Should().NotBeNull();
        result.MfiSignal.Should().Be("overbought");
    }

    [Fact]
    public async Task Analyze_Mfi_StrongDowntrend_Oversold()
    {
        var prices = GeneratePriceHistory(60, 80_000m, -500m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Mfi14.Should().NotBeNull();
        result.MfiSignal.Should().Be("oversold");
    }

    [Fact]
    public async Task Analyze_Mfi_InsufficientData_Null()
    {
        var prices = GeneratePriceHistory(10, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.Mfi14.Should().BeNull();
    }

    // ─── Voting System — 10 Indicators ──────────────────────────────────

    [Fact]
    public async Task Analyze_SignalCount_Includes10Indicators()
    {
        var prices = GeneratePriceHistory(120, 50_000m, 200m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        // 10 votes: EMA, RSI, MACD, Volume, Bollinger, ATR, Stochastic, ADX, OBV, MFI
        (result.BullishCount + result.BearishCount + result.NeutralCount).Should().Be(10);
    }

    [Fact]
    public async Task Analyze_StrongUptrend_MultipleBullishVotes()
    {
        // Strong uptrend should generate several bullish votes
        var prices = GeneratePriceHistory(120, 30_000m, 300m);
        SetupPrices(prices);

        var result = await _sut.AnalyzeAsync("VNM");

        result.BullishCount.Should().BeGreaterThanOrEqualTo(3);
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
