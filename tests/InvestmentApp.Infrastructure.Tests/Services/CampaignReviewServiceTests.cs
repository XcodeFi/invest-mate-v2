using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class CampaignReviewServiceTests
{
    private readonly CampaignReviewService _sut = new();

    private static TradePlan CreatePlan(decimal entryPrice = 80_000m, decimal target = 90_000m)
    {
        return new TradePlan("user-1", "VNM", "Buy", entryPrice, 75_000m, target, 100);
    }

    private static Trade CreateBuy(decimal price, decimal qty, decimal fee = 0, decimal tax = 0, DateTime? date = null)
    {
        return new Trade("port-1", "VNM", TradeType.BUY, qty, price, fee, tax, date ?? DateTime.UtcNow.AddDays(-30));
    }

    private static Trade CreateSell(decimal price, decimal qty, decimal fee = 0, decimal tax = 0, DateTime? date = null)
    {
        return new Trade("port-1", "VNM", TradeType.SELL, qty, price, fee, tax, date ?? DateTime.UtcNow);
    }

    [Fact]
    public void Calculate_BuysAndSells_ShouldComputeCorrectPnL()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: new DateTime(2026, 1, 1)),
            CreateSell(88_000m, 100, date: new DateTime(2026, 1, 31))
        };

        var result = _sut.CalculateMetrics(plan, trades);

        result.TotalInvested.Should().Be(8_000_000m);   // 80k × 100
        result.TotalReturned.Should().Be(8_800_000m);    // 88k × 100
        result.PnLAmount.Should().Be(800_000m);
        result.PnLPercent.Should().Be(10m);
        result.HoldingDays.Should().Be(30);
        result.PnLPerDay.Should().Be(26_667m);           // 800k / 30 rounded
    }

    [Fact]
    public void Calculate_SingleDayTrade_HoldingDaysMin1()
    {
        var plan = CreatePlan();
        var sameDay = new DateTime(2026, 3, 15);
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: sameDay),
            CreateSell(82_000m, 100, date: sameDay)
        };

        var result = _sut.CalculateMetrics(plan, trades);

        result.HoldingDays.Should().Be(1);
        result.PnLAmount.Should().Be(200_000m);
        result.PnLPerDay.Should().Be(200_000m);
    }

    [Fact]
    public void Calculate_LossTrade_NegativeValues()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: new DateTime(2026, 1, 1)),
            CreateSell(72_000m, 100, date: new DateTime(2026, 2, 1))
        };

        var result = _sut.CalculateMetrics(plan, trades);

        result.PnLAmount.Should().Be(-800_000m);
        result.PnLPercent.Should().Be(-10m);
        result.PnLPerDay.Should().BeNegative();
    }

    [Fact]
    public void Calculate_WithFees_ShouldSubtractFromPnL()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 100, fee: 50_000m, tax: 10_000m, date: new DateTime(2026, 1, 1)),
            CreateSell(88_000m, 100, fee: 50_000m, tax: 10_000m, date: new DateTime(2026, 1, 31))
        };

        var result = _sut.CalculateMetrics(plan, trades);

        // Invested: 80k×100 + 50k + 10k = 8,060,000
        // Returned: 88k×100 - 50k - 10k = 8,740,000
        // PnL: 8,740,000 - 8,060,000 = 680,000
        result.TotalInvested.Should().Be(8_060_000m);
        result.TotalReturned.Should().Be(8_740_000m);
        result.PnLAmount.Should().Be(680_000m);
        result.TotalFees.Should().Be(120_000m);
    }

    [Fact]
    public void Calculate_TargetAchievement_RelativeToPlannedTarget()
    {
        // Plan: entry=80k, target=90k → planned target% = 12.5%
        var plan = CreatePlan(entryPrice: 80_000m, target: 90_000m);
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: new DateTime(2026, 1, 1)),
            CreateSell(85_000m, 100, date: new DateTime(2026, 2, 1))
        };

        var result = _sut.CalculateMetrics(plan, trades);

        // Actual PnL% = 6.25%, planned target% = 12.5%
        // Achievement = 6.25 / 12.5 × 100 = 50%
        result.TargetAchievementPercent.Should().Be(50m);
    }

    [Fact]
    public void Calculate_AnnualizedReturn_ShortHolding_ShouldBeZero()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: new DateTime(2026, 3, 1)),
            CreateSell(82_000m, 100, date: new DateTime(2026, 3, 4))  // 3 days < 7
        };

        var result = _sut.CalculateMetrics(plan, trades);

        // Holding < 7 days → annualized return = 0 (too short to annualize meaningfully)
        result.AnnualizedReturnPercent.Should().Be(0m);
    }

    [Fact]
    public void Calculate_AnnualizedReturn_NormalHolding_ShouldCompute()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 100, date: new DateTime(2026, 1, 1)),
            CreateSell(88_000m, 100, date: new DateTime(2026, 4, 1))  // ~90 days
        };

        var result = _sut.CalculateMetrics(plan, trades);

        // PnL% = 10%, holding ~90 days → annualized should be > 10%
        result.AnnualizedReturnPercent.Should().BeGreaterThan(10m);
    }

    [Fact]
    public void Calculate_MultipleBuysAndSells_AllTrades()
    {
        var plan = CreatePlan();
        var trades = new[]
        {
            CreateBuy(80_000m, 50, date: new DateTime(2026, 1, 1)),
            CreateBuy(78_000m, 50, date: new DateTime(2026, 1, 15)),
            CreateSell(85_000m, 60, date: new DateTime(2026, 2, 1)),
            CreateSell(87_000m, 40, date: new DateTime(2026, 2, 15))
        };

        var result = _sut.CalculateMetrics(plan, trades);

        // Invested: 80k×50 + 78k×50 = 4M + 3.9M = 7,900,000
        // Returned: 85k×60 + 87k×40 = 5.1M + 3.48M = 8,580,000
        // PnL: 680,000
        result.TotalInvested.Should().Be(7_900_000m);
        result.TotalReturned.Should().Be(8_580_000m);
        result.PnLAmount.Should().Be(680_000m);
        result.HoldingDays.Should().Be(45);  // Jan 1 → Feb 15
    }

    [Fact]
    public void Calculate_NoTrades_ShouldReturnZeros()
    {
        var plan = CreatePlan();
        var trades = Array.Empty<Trade>();

        var result = _sut.CalculateMetrics(plan, trades);

        result.PnLAmount.Should().Be(0m);
        result.PnLPercent.Should().Be(0m);
        result.HoldingDays.Should().Be(1);
        result.TotalInvested.Should().Be(0m);
    }
}
