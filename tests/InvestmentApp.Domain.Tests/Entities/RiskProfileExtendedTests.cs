using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class RiskProfileExtendedTests
{
    [Fact]
    public void Constructor_NullableParams_DefaultsNull()
    {
        var profile = new RiskProfile("port-1", "user-1");

        profile.MaxDailyTrades.Should().BeNull();
        profile.DailyLossLimitPercent.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDailyLimits_SetsValues()
    {
        var profile = new RiskProfile("port-1", "user-1",
            maxDailyTrades: 5,
            dailyLossLimitPercent: 3.0m);

        profile.MaxDailyTrades.Should().Be(5);
        profile.DailyLossLimitPercent.Should().Be(3.0m);
    }

    [Fact]
    public void Update_NewDailyFields_UpdatesCorrectly()
    {
        var profile = new RiskProfile("port-1", "user-1");

        profile.Update(maxDailyTrades: 10, dailyLossLimitPercent: 2.5m);

        profile.MaxDailyTrades.Should().Be(10);
        profile.DailyLossLimitPercent.Should().Be(2.5m);
    }

    [Fact]
    public void Update_NullDailyFields_DoesNotOverwrite()
    {
        var profile = new RiskProfile("port-1", "user-1", maxDailyTrades: 5, dailyLossLimitPercent: 2.0m);

        profile.Update(maxPositionSizePercent: 30m);

        profile.MaxDailyTrades.Should().Be(5);
        profile.DailyLossLimitPercent.Should().Be(2.0m);
        profile.MaxPositionSizePercent.Should().Be(30m);
    }

    [Fact]
    public void Update_ExistingFieldsUnchanged_WhenOnlyNewFieldsSet()
    {
        var profile = new RiskProfile("port-1", "user-1",
            maxPositionSizePercent: 20m,
            defaultRiskRewardRatio: 2.0m);

        profile.Update(maxDailyTrades: 8);

        profile.MaxPositionSizePercent.Should().Be(20m);
        profile.DefaultRiskRewardRatio.Should().Be(2.0m);
        profile.MaxDailyTrades.Should().Be(8);
    }
}
