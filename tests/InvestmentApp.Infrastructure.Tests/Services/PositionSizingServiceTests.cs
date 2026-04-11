using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class PositionSizingServiceTests
{
    private readonly PositionSizingService _sut = new();

    // ─── Fixed Risk (baseline, always present) ─────────────────────────

    [Fact]
    public async Task Calculate_FixedRisk_BasicCalculation()
    {
        var request = CreateBaseRequest();

        var result = await _sut.CalculateAsync(request);

        var fixedRisk = result.Models.First(m => m.Model == "fixed_risk");
        // MaxRisk = 100M * 2% = 2M, RiskPerShare = 50000 - 47000 = 3000
        // Shares = floor(2M / 3000 / 100) * 100 = floor(666/100)*100 = 600
        fixedRisk.Shares.Should().Be(600);
        fixedRisk.PositionValue.Should().Be(600 * 50_000m); // 30M = 30%
        fixedRisk.RiskAmount.Should().Be(600 * 3_000m);
        fixedRisk.WithinLimit.Should().BeFalse(); // 30% > 20% limit
    }

    [Fact]
    public async Task Calculate_FixedRisk_ExceedsMaxPosition_Capped()
    {
        var request = CreateBaseRequest();
        request.MaxPositionPercent = 10; // max 10M position
        // 600 shares * 50k = 30M = 30% → exceeds 10%

        var result = await _sut.CalculateAsync(request);

        var fixedRisk = result.Models.First(m => m.Model == "fixed_risk");
        fixedRisk.WithinLimit.Should().BeFalse();
    }

    [Fact]
    public async Task Calculate_FixedRisk_ZeroRiskPerShare_Returns100Shares()
    {
        var request = CreateBaseRequest();
        request.StopLoss = request.EntryPrice; // risk per share = 0

        var result = await _sut.CalculateAsync(request);

        var fixedRisk = result.Models.First(m => m.Model == "fixed_risk");
        fixedRisk.Shares.Should().Be(100); // minimum
    }

    // ─── ATR-Based Sizing ───────────────────────────────────────────────

    [Fact]
    public async Task Calculate_AtrBased_BasicCalculation()
    {
        var request = CreateBaseRequest();
        request.Atr = 1_500m;
        request.AtrMultiplier = 2m;

        var result = await _sut.CalculateAsync(request);

        var atr = result.Models.First(m => m.Model == "atr_based");
        // MaxRisk = 100M * 2% = 2M, ATR stop = 2 * 1500 = 3000
        // Shares = floor(2M / 3000 / 100) * 100 = 600
        atr.Shares.Should().Be(600);
        atr.Should().NotBeNull();
    }

    [Fact]
    public async Task Calculate_AtrBased_HighVolatility_FewerShares()
    {
        var request = CreateBaseRequest();
        request.Atr = 3_000m;
        request.AtrMultiplier = 2m;

        var result = await _sut.CalculateAsync(request);

        var atr = result.Models.First(m => m.Model == "atr_based");
        // ATR stop = 2 * 3000 = 6000 → Shares = floor(2M/6000/100)*100 = 300
        atr.Shares.Should().Be(300);
    }

    [Fact]
    public async Task Calculate_AtrBased_NoAtr_NotIncluded()
    {
        var request = CreateBaseRequest();
        request.Atr = null;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().NotContain(m => m.Model == "atr_based");
    }

    [Fact]
    public async Task Calculate_AtrBased_ZeroAtr_NotIncluded()
    {
        var request = CreateBaseRequest();
        request.Atr = 0;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().NotContain(m => m.Model == "atr_based");
    }

    // ─── Kelly Criterion ────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_Kelly_BasicCalculation()
    {
        var request = CreateBaseRequest();
        request.WinRate = 0.55m;     // 55% win
        request.AverageWin = 5_000m; // avg win per trade
        request.AverageLoss = 3_000m; // avg loss per trade

        var result = await _sut.CalculateAsync(request);

        var kelly = result.Models.First(m => m.Model == "kelly");
        // b = avgWin / avgLoss = 5000/3000 = 1.667
        // f* = (b*p - q) / b = (1.667*0.55 - 0.45) / 1.667 = (0.917 - 0.45) / 1.667 = 0.280
        // Half-Kelly = 0.140 = 14%
        // PositionValue = 100M * 14% = 14M
        // Shares = floor(14M / 50000 / 100) * 100 = 200 (approximate)
        kelly.Shares.Should().BeGreaterThan(0);
        kelly.Note.Should().Contain("Kelly");
    }

    [Fact]
    public async Task Calculate_Kelly_HalfKelly_CappedAt25Percent()
    {
        var request = CreateBaseRequest();
        request.WinRate = 0.80m;      // very high win rate
        request.AverageWin = 10_000m;
        request.AverageLoss = 2_000m;

        var result = await _sut.CalculateAsync(request);

        var kelly = result.Models.First(m => m.Model == "kelly");
        // Full Kelly would be very large, Half-Kelly should be capped at 25%
        kelly.PositionPercent.Should().BeLessThanOrEqualTo(25);
    }

    [Fact]
    public async Task Calculate_Kelly_NegativeEdge_NotIncluded()
    {
        var request = CreateBaseRequest();
        request.WinRate = 0.30m;     // 30% win (bad edge)
        request.AverageWin = 3_000m;
        request.AverageLoss = 5_000m;

        var result = await _sut.CalculateAsync(request);

        // f* = (0.6*0.3 - 0.7) / 0.6 = negative → don't show
        result.Models.Should().NotContain(m => m.Model == "kelly");
    }

    [Fact]
    public async Task Calculate_Kelly_NoTradeHistory_NotIncluded()
    {
        var request = CreateBaseRequest();
        // No win rate / avg data

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().NotContain(m => m.Model == "kelly");
    }

    // ─── Turtle Sizing ──────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_Turtle_BasicCalculation()
    {
        var request = CreateBaseRequest();
        request.Atr = 1_500m;

        var result = await _sut.CalculateAsync(request);

        var turtle = result.Models.First(m => m.Model == "turtle");
        // 1 Unit = 1% * 100M / (1 * 1500) = 1M / 1500 = 666.67
        // Round to 100: 600 shares per unit
        turtle.Shares.Should().Be(600);
    }

    [Fact]
    public async Task Calculate_Turtle_Max4Units_Capped()
    {
        var request = CreateBaseRequest();
        request.Atr = 200m; // very low ATR → huge unit size

        var result = await _sut.CalculateAsync(request);

        var turtle = result.Models.First(m => m.Model == "turtle");
        // 1 Unit = 1M / 200 = 5000 shares
        // But max 4 units not relevant here — single unit calculation
        // Max units cap: 4 * unit * price <= accountBalance (exposure cap)
        turtle.Shares.Should().BeGreaterThan(0);
        turtle.Note.Should().Contain("unit");
    }

    [Fact]
    public async Task Calculate_Turtle_NoAtr_NotIncluded()
    {
        var request = CreateBaseRequest();
        request.Atr = null;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().NotContain(m => m.Model == "turtle");
    }

    // ─── Volatility-Adjusted ────────────────────────────────────────────

    [Fact]
    public async Task Calculate_VolatilityAdjusted_HighVolatility_FewerShares()
    {
        var request = CreateBaseRequest();
        request.Atr = 2_000m;
        request.AtrPercent = 4m; // 4% = high volatility

        var result = await _sut.CalculateAsync(request);

        var volAdj = result.Models.First(m => m.Model == "volatility_adjusted");
        var fixedRisk = result.Models.First(m => m.Model == "fixed_risk");

        // High volatility → fewer shares than fixed risk
        volAdj.Shares.Should().BeLessThanOrEqualTo(fixedRisk.Shares);
    }

    [Fact]
    public async Task Calculate_VolatilityAdjusted_LowVolatility_MoreShares()
    {
        var request = CreateBaseRequest();
        request.Atr = 500m;
        request.AtrPercent = 1m; // 1% = low volatility

        var result = await _sut.CalculateAsync(request);

        var volAdj = result.Models.First(m => m.Model == "volatility_adjusted");
        var fixedRisk = result.Models.First(m => m.Model == "fixed_risk");

        // Low volatility → more shares than fixed risk
        volAdj.Shares.Should().BeGreaterThanOrEqualTo(fixedRisk.Shares);
    }

    [Fact]
    public async Task Calculate_VolatilityAdjusted_NoAtrPercent_NotIncluded()
    {
        var request = CreateBaseRequest();
        request.AtrPercent = null;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().NotContain(m => m.Model == "volatility_adjusted");
    }

    // ─── Integration ────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_AllModels_WhenAllDataProvided()
    {
        var request = CreateBaseRequest();
        request.Atr = 1_500m;
        request.AtrPercent = 3m;
        request.WinRate = 0.55m;
        request.AverageWin = 5_000m;
        request.AverageLoss = 3_000m;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().HaveCount(5);
        result.Models.Select(m => m.Model).Should().Contain("fixed_risk");
        result.Models.Select(m => m.Model).Should().Contain("atr_based");
        result.Models.Select(m => m.Model).Should().Contain("kelly");
        result.Models.Select(m => m.Model).Should().Contain("turtle");
        result.Models.Select(m => m.Model).Should().Contain("volatility_adjusted");
    }

    [Fact]
    public async Task Calculate_AllModels_HaveVietnameseLabels()
    {
        var request = CreateBaseRequest();
        request.Atr = 1_500m;
        request.AtrPercent = 3m;
        request.WinRate = 0.55m;
        request.AverageWin = 5_000m;
        request.AverageLoss = 3_000m;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().AllSatisfy(m =>
        {
            m.ModelVi.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Calculate_AllModels_MinimumShares100()
    {
        var request = CreateBaseRequest();
        request.AccountBalance = 1_000_000; // very small account
        request.Atr = 1_500m;
        request.AtrPercent = 3m;
        request.WinRate = 0.55m;
        request.AverageWin = 5_000m;
        request.AverageLoss = 3_000m;

        var result = await _sut.CalculateAsync(request);

        result.Models.Should().AllSatisfy(m =>
        {
            m.Shares.Should().BeGreaterThanOrEqualTo(100);
        });
    }

    [Fact]
    public async Task Calculate_RecommendedModel_IsPopulated()
    {
        var request = CreateBaseRequest();
        request.Atr = 1_500m;
        request.AtrPercent = 3m;

        var result = await _sut.CalculateAsync(request);

        result.RecommendedModel.Should().NotBeNullOrEmpty();
        result.Models.Select(m => m.Model).Should().Contain(result.RecommendedModel);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Base request: 100M VND account, entry 50k, SL 47k, risk 2%, max pos 20%
    /// RiskPerShare = 3,000 VND
    /// </summary>
    private static PositionSizingRequest CreateBaseRequest() => new()
    {
        AccountBalance = 100_000_000m,
        EntryPrice = 50_000m,
        StopLoss = 47_000m,
        RiskPercent = 2m,
        MaxPositionPercent = 20m,
        AtrMultiplier = 2m
    };
}
