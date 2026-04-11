using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class StrategyTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateStrategy()
    {
        // Arrange
        var userId = "user-1";
        var name = "Breakout Strategy";
        var description = "Buy on breakout above resistance";
        var entryRules = "Price breaks above 20-day high";
        var exitRules = "Price drops below 10-day low";
        var riskRules = "Max 2% per trade";
        var timeFrame = "Swing";
        var marketCondition = "Trending";

        // Act
        var strategy = new Strategy(userId, name, description,
            entryRules, exitRules, riskRules, timeFrame, marketCondition);

        // Assert
        strategy.Id.Should().NotBeNullOrEmpty();
        strategy.UserId.Should().Be(userId);
        strategy.Name.Should().Be(name);
        strategy.Description.Should().Be(description);
        strategy.EntryRules.Should().Be(entryRules);
        strategy.ExitRules.Should().Be(exitRules);
        strategy.RiskRules.Should().Be(riskRules);
        strategy.TimeFrame.Should().Be(timeFrame);
        strategy.MarketCondition.Should().Be(marketCondition);
        strategy.SuggestedSlPercent.Should().BeNull();
        strategy.SuggestedRrRatio.Should().BeNull();
        strategy.IsActive.Should().BeTrue();
        strategy.IsDeleted.Should().BeFalse();
        strategy.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        strategy.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        strategy.Version.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithSuggestedSlAndRr_ShouldSetOptionalFields()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending",
            suggestedSlPercent: 5m, suggestedRrRatio: 2.5m, suggestedSlMethod: "atr");

        // Assert
        strategy.SuggestedSlPercent.Should().Be(5m);
        strategy.SuggestedRrRatio.Should().Be(2.5m);
        strategy.SuggestedSlMethod.Should().Be("atr");
    }

    [Fact]
    public void Constructor_WithoutSuggestedSlMethod_ShouldDefaultToNull()
    {
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        strategy.SuggestedSlMethod.Should().BeNull();
    }

    #endregion

    #region Constructor — Defaults

    [Fact]
    public void Constructor_NullDescription_ShouldDefaultToEmpty()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", null!,
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Assert
        strategy.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_NullEntryRules_ShouldDefaultToEmpty()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            null!, "Exit", "Risk", "Swing", "Trending");

        // Assert
        strategy.EntryRules.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_NullExitRules_ShouldDefaultToEmpty()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", null!, "Risk", "Swing", "Trending");

        // Assert
        strategy.ExitRules.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_NullRiskRules_ShouldDefaultToEmpty()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", null!, "Swing", "Trending");

        // Assert
        strategy.RiskRules.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_NullTimeFrame_ShouldDefaultToSwing()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", null!, "Trending");

        // Assert
        strategy.TimeFrame.Should().Be("Swing");
    }

    [Fact]
    public void Constructor_NullMarketCondition_ShouldDefaultToTrending()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", null!);

        // Assert
        strategy.MarketCondition.Should().Be("Trending");
    }

    [Fact]
    public void Constructor_AllNullableFieldsNull_ShouldUseAllDefaults()
    {
        // Act
        var strategy = new Strategy("user-1", "Test", null!,
            null!, null!, null!, null!, null!);

        // Assert
        strategy.Description.Should().Be(string.Empty);
        strategy.EntryRules.Should().Be(string.Empty);
        strategy.ExitRules.Should().Be(string.Empty);
        strategy.RiskRules.Should().Be(string.Empty);
        strategy.TimeFrame.Should().Be("Swing");
        strategy.MarketCondition.Should().Be("Trending");
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Strategy(null!, "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Strategy("user-1", null!, "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    #endregion

    #region Update — Partial

    [Fact]
    public void Update_OnlyName_ShouldUpdateNameOnly()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Old Name", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");
        var originalDescription = strategy.Description;
        var originalEntryRules = strategy.EntryRules;

        // Act
        strategy.Update(name: "New Name");

        // Assert
        strategy.Name.Should().Be("New Name");
        strategy.Description.Should().Be(originalDescription);
        strategy.EntryRules.Should().Be(originalEntryRules);
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyDescription_ShouldUpdateDescriptionOnly()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Old Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Act
        strategy.Update(description: "New Description");

        // Assert
        strategy.Description.Should().Be("New Description");
        strategy.Name.Should().Be("Test");
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyIsActive_ShouldToggleActiveState()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");
        strategy.IsActive.Should().BeTrue();

        // Act
        strategy.Update(isActive: false);

        // Assert
        strategy.IsActive.Should().BeFalse();
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlySuggestedSlPercent_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Act
        strategy.Update(suggestedSlPercent: 7m);

        // Assert
        strategy.SuggestedSlPercent.Should().Be(7m);
        strategy.SuggestedRrRatio.Should().BeNull();
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlySuggestedRrRatio_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Act
        strategy.Update(suggestedRrRatio: 3.0m);

        // Assert
        strategy.SuggestedRrRatio.Should().Be(3.0m);
        strategy.SuggestedSlPercent.Should().BeNull();
        strategy.Version.Should().Be(1);
    }

    #endregion

    #region Update — All Fields

    [Fact]
    public void Update_AllFields_ShouldUpdateEverything()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Old", "Old Desc",
            "Old Entry", "Old Exit", "Old Risk", "Swing", "Trending");
        var beforeUpdate = strategy.UpdatedAt;

        // Act
        strategy.Update(
            name: "New Name",
            description: "New Desc",
            entryRules: "New Entry",
            exitRules: "New Exit",
            riskRules: "New Risk",
            timeFrame: "Scalping",
            marketCondition: "Volatile",
            isActive: false,
            suggestedSlPercent: 5m,
            suggestedRrRatio: 2.0m,
            suggestedSlMethod: "support");

        // Assert
        strategy.Name.Should().Be("New Name");
        strategy.Description.Should().Be("New Desc");
        strategy.EntryRules.Should().Be("New Entry");
        strategy.ExitRules.Should().Be("New Exit");
        strategy.RiskRules.Should().Be("New Risk");
        strategy.TimeFrame.Should().Be("Scalping");
        strategy.MarketCondition.Should().Be("Volatile");
        strategy.IsActive.Should().BeFalse();
        strategy.SuggestedSlPercent.Should().Be(5m);
        strategy.SuggestedRrRatio.Should().Be(2.0m);
        strategy.SuggestedSlMethod.Should().Be("support");
        strategy.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void Update_CalledMultipleTimes_ShouldIncrementVersionEachTime()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Act
        strategy.Update(name: "Update 1");
        strategy.Update(name: "Update 2");
        strategy.Update(name: "Update 3");

        // Assert
        strategy.Version.Should().Be(3);
        strategy.Name.Should().Be("Update 3");
    }

    [Fact]
    public void Update_NoParameters_ShouldStillIncrementVersionAndUpdateTimestamp()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");
        var originalName = strategy.Name;

        // Act
        strategy.Update();

        // Assert
        strategy.Name.Should().Be(originalName);
        strategy.Version.Should().Be(1);
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");
        strategy.IsDeleted.Should().BeFalse();

        // Act
        strategy.SoftDelete();

        // Assert
        strategy.IsDeleted.Should().BeTrue();
        strategy.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SoftDelete_ShouldIncrementVersion()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");

        // Act
        strategy.SoftDelete();

        // Assert
        strategy.Version.Should().Be(1);
    }

    [Fact]
    public void SoftDelete_AfterUpdate_ShouldHaveCorrectVersion()
    {
        // Arrange
        var strategy = new Strategy("user-1", "Test", "Desc",
            "Entry", "Exit", "Risk", "Swing", "Trending");
        strategy.Update(name: "Updated");

        // Act
        strategy.SoftDelete();

        // Assert
        strategy.Version.Should().Be(2);
        strategy.IsDeleted.Should().BeTrue();
    }

    #endregion
}
