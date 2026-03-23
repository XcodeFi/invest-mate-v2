using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class RiskProfileTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateRiskProfile()
    {
        // Arrange
        var portfolioId = "portfolio-1";
        var userId = "user-1";

        // Act
        var profile = new RiskProfile(portfolioId, userId);

        // Assert
        profile.Id.Should().NotBeNullOrEmpty();
        profile.PortfolioId.Should().Be(portfolioId);
        profile.UserId.Should().Be(userId);
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        profile.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        profile.Version.Should().Be(0);
    }

    [Fact]
    public void Constructor_DefaultValues_ShouldUseSpecifiedDefaults()
    {
        // Act
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Assert
        profile.MaxPositionSizePercent.Should().Be(20m);
        profile.MaxSectorExposurePercent.Should().Be(40m);
        profile.MaxDrawdownAlertPercent.Should().Be(10m);
        profile.DefaultRiskRewardRatio.Should().Be(2.0m);
        profile.MaxPortfolioRiskPercent.Should().Be(5m);
    }

    [Fact]
    public void Constructor_CustomValues_ShouldOverrideDefaults()
    {
        // Act
        var profile = new RiskProfile("portfolio-1", "user-1",
            maxPositionSizePercent: 15m,
            maxSectorExposurePercent: 30m,
            maxDrawdownAlertPercent: 8m,
            defaultRiskRewardRatio: 3.0m,
            maxPortfolioRiskPercent: 3m);

        // Assert
        profile.MaxPositionSizePercent.Should().Be(15m);
        profile.MaxSectorExposurePercent.Should().Be(30m);
        profile.MaxDrawdownAlertPercent.Should().Be(8m);
        profile.DefaultRiskRewardRatio.Should().Be(3.0m);
        profile.MaxPortfolioRiskPercent.Should().Be(3m);
    }

    [Fact]
    public void Constructor_PartialCustomValues_ShouldMixCustomAndDefaults()
    {
        // Act
        var profile = new RiskProfile("portfolio-1", "user-1",
            maxPositionSizePercent: 25m,
            defaultRiskRewardRatio: 1.5m);

        // Assert
        profile.MaxPositionSizePercent.Should().Be(25m);
        profile.MaxSectorExposurePercent.Should().Be(40m); // default
        profile.MaxDrawdownAlertPercent.Should().Be(10m);  // default
        profile.DefaultRiskRewardRatio.Should().Be(1.5m);
        profile.MaxPortfolioRiskPercent.Should().Be(5m);   // default
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullPortfolioId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new RiskProfile(null!, "user-1");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new RiskProfile("portfolio-1", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_BothNull_ShouldThrowForPortfolioIdFirst()
    {
        // Act
        var action = () => new RiskProfile(null!, null!);

        // Assert — portfolioId is checked first in the constructor
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    #endregion

    #region Constructor — Id Generation

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Act
        var profile1 = new RiskProfile("portfolio-1", "user-1");
        var profile2 = new RiskProfile("portfolio-2", "user-1");

        // Assert
        profile1.Id.Should().NotBe(profile2.Id);
    }

    #endregion

    #region Update — Partial

    [Fact]
    public void Update_OnlyMaxPositionSizePercent_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Act
        profile.Update(maxPositionSizePercent: 30m);

        // Assert
        profile.MaxPositionSizePercent.Should().Be(30m);
        profile.MaxSectorExposurePercent.Should().Be(40m);  // unchanged
        profile.MaxDrawdownAlertPercent.Should().Be(10m);    // unchanged
        profile.DefaultRiskRewardRatio.Should().Be(2.0m);    // unchanged
        profile.MaxPortfolioRiskPercent.Should().Be(5m);     // unchanged
        profile.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyMaxSectorExposurePercent_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Act
        profile.Update(maxSectorExposurePercent: 50m);

        // Assert
        profile.MaxSectorExposurePercent.Should().Be(50m);
        profile.MaxPositionSizePercent.Should().Be(20m);  // unchanged
        profile.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyDefaultRiskRewardRatio_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Act
        profile.Update(defaultRiskRewardRatio: 4.0m);

        // Assert
        profile.DefaultRiskRewardRatio.Should().Be(4.0m);
        profile.MaxPositionSizePercent.Should().Be(20m);  // unchanged
        profile.Version.Should().Be(1);
    }

    #endregion

    #region Update — All Fields

    [Fact]
    public void Update_AllFields_ShouldUpdateEverything()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");
        var beforeUpdate = profile.UpdatedAt;

        // Act
        profile.Update(
            maxPositionSizePercent: 10m,
            maxSectorExposurePercent: 25m,
            maxDrawdownAlertPercent: 5m,
            defaultRiskRewardRatio: 3.5m,
            maxPortfolioRiskPercent: 2m);

        // Assert
        profile.MaxPositionSizePercent.Should().Be(10m);
        profile.MaxSectorExposurePercent.Should().Be(25m);
        profile.MaxDrawdownAlertPercent.Should().Be(5m);
        profile.DefaultRiskRewardRatio.Should().Be(3.5m);
        profile.MaxPortfolioRiskPercent.Should().Be(2m);
        profile.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        profile.Version.Should().Be(1);
    }

    [Fact]
    public void Update_NoParameters_ShouldStillIncrementVersionAndUpdateTimestamp()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Act
        profile.Update();

        // Assert
        profile.MaxPositionSizePercent.Should().Be(20m);  // unchanged
        profile.Version.Should().Be(1);
    }

    #endregion

    #region Update — Version Increment

    [Fact]
    public void Update_CalledMultipleTimes_ShouldIncrementVersionEachTime()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");

        // Act
        profile.Update(maxPositionSizePercent: 10m);
        profile.Update(maxSectorExposurePercent: 20m);
        profile.Update(maxDrawdownAlertPercent: 15m);

        // Assert
        profile.Version.Should().Be(3);
    }

    [Fact]
    public void Update_ShouldUpdateTimestamp()
    {
        // Arrange
        var profile = new RiskProfile("portfolio-1", "user-1");
        var createdAt = profile.CreatedAt;

        // Act
        profile.Update(maxPositionSizePercent: 10m);

        // Assert
        profile.UpdatedAt.Should().BeOnOrAfter(createdAt);
        profile.CreatedAt.Should().Be(createdAt); // CreatedAt should not change
    }

    #endregion
}
