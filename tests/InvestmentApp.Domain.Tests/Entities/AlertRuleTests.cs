using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class AlertRuleTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateAlertRule()
    {
        // Arrange
        var userId = "user-1";
        var name = "Price Alert VNM";
        var alertType = "PriceAlert";
        var condition = "Above";
        var threshold = 90000m;
        var channel = "Email";

        // Act
        var rule = new AlertRule(userId, name, alertType, condition, threshold, channel);

        // Assert
        rule.Id.Should().NotBeNullOrEmpty();
        rule.UserId.Should().Be(userId);
        rule.Name.Should().Be(name);
        rule.AlertType.Should().Be(alertType);
        rule.Condition.Should().Be(condition);
        rule.Threshold.Should().Be(threshold);
        rule.Channel.Should().Be(channel);
        rule.IsActive.Should().BeTrue();
        rule.IsDeleted.Should().BeFalse();
        rule.LastTriggeredAt.Should().BeNull();
        rule.PortfolioId.Should().BeNull();
        rule.Symbol.Should().BeNull();
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        rule.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        rule.Version.Should().Be(0);
    }

    [Fact]
    public void Constructor_AllParameters_ShouldSetOptionalFields()
    {
        // Arrange
        var portfolioId = "portfolio-1";
        var symbol = "VNM";

        // Act
        var rule = new AlertRule("user-1", "Drawdown Alert", "DrawdownAlert",
            "Exceeds", 15m, "InApp", portfolioId, symbol);

        // Assert
        rule.PortfolioId.Should().Be(portfolioId);
        rule.Symbol.Should().Be(symbol);
    }

    #endregion

    #region Constructor — Defaults

    [Fact]
    public void Constructor_NullCondition_ShouldDefaultToExceeds()
    {
        // Act
        var rule = new AlertRule("user-1", "Test Alert", "PriceAlert",
            null!, 100m, "Email");

        // Assert
        rule.Condition.Should().Be("Exceeds");
    }

    [Fact]
    public void Constructor_NullChannel_ShouldDefaultToInApp()
    {
        // Act
        var rule = new AlertRule("user-1", "Test Alert", "PriceAlert",
            "Above", 100m, null!);

        // Assert
        rule.Channel.Should().Be("InApp");
    }

    [Fact]
    public void Constructor_NullConditionAndChannel_ShouldUseBothDefaults()
    {
        // Act
        var rule = new AlertRule("user-1", "Test Alert", "PriceAlert",
            null!, 100m, null!);

        // Assert
        rule.Condition.Should().Be("Exceeds");
        rule.Channel.Should().Be("InApp");
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertRule(null!, "Test", "PriceAlert",
            "Above", 100m, "InApp");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertRule("user-1", null!, "PriceAlert",
            "Above", 100m, "InApp");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_NullAlertType_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertRule("user-1", "Test", null!,
            "Above", 100m, "InApp");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("alertType");
    }

    #endregion

    #region Update — Partial

    [Fact]
    public void Update_OnlyName_ShouldUpdateNameOnly()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Old Name", "PriceAlert",
            "Above", 100m, "InApp");
        var originalAlertType = rule.AlertType;
        var originalCondition = rule.Condition;
        var originalThreshold = rule.Threshold;
        var originalChannel = rule.Channel;

        // Act
        rule.Update(name: "New Name");

        // Assert
        rule.Name.Should().Be("New Name");
        rule.AlertType.Should().Be(originalAlertType);
        rule.Condition.Should().Be(originalCondition);
        rule.Threshold.Should().Be(originalThreshold);
        rule.Channel.Should().Be(originalChannel);
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyThreshold_ShouldUpdateThresholdOnly()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        var originalName = rule.Name;

        // Act
        rule.Update(threshold: 200m);

        // Assert
        rule.Threshold.Should().Be(200m);
        rule.Name.Should().Be(originalName);
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void Update_OnlyIsActive_ShouldToggleActiveState()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        rule.IsActive.Should().BeTrue();

        // Act
        rule.Update(isActive: false);

        // Assert
        rule.IsActive.Should().BeFalse();
        rule.Version.Should().Be(1);
    }

    #endregion

    #region Update — All Fields

    [Fact]
    public void Update_AllFields_ShouldUpdateEverything()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Old Name", "PriceAlert",
            "Above", 100m, "InApp");
        var beforeUpdate = rule.UpdatedAt;

        // Act
        rule.Update(
            name: "New Name",
            alertType: "DrawdownAlert",
            condition: "Below",
            threshold: 200m,
            channel: "Email",
            isActive: false,
            symbol: "FPT",
            portfolioId: "portfolio-2");

        // Assert
        rule.Name.Should().Be("New Name");
        rule.AlertType.Should().Be("DrawdownAlert");
        rule.Condition.Should().Be("Below");
        rule.Threshold.Should().Be(200m);
        rule.Channel.Should().Be("Email");
        rule.IsActive.Should().BeFalse();
        rule.Symbol.Should().Be("FPT");
        rule.PortfolioId.Should().Be("portfolio-2");
        rule.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void Update_CalledMultipleTimes_ShouldIncrementVersionEachTime()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");

        // Act
        rule.Update(name: "Update 1");
        rule.Update(name: "Update 2");
        rule.Update(name: "Update 3");

        // Assert
        rule.Version.Should().Be(3);
        rule.Name.Should().Be("Update 3");
    }

    [Fact]
    public void Update_NoParameters_ShouldStillIncrementVersionAndUpdateTimestamp()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        var originalName = rule.Name;
        var originalThreshold = rule.Threshold;

        // Act
        rule.Update();

        // Assert
        rule.Name.Should().Be(originalName);
        rule.Threshold.Should().Be(originalThreshold);
        rule.Version.Should().Be(1);
    }

    #endregion

    #region MarkTriggered

    [Fact]
    public void MarkTriggered_ShouldSetLastTriggeredAt()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        rule.LastTriggeredAt.Should().BeNull();

        // Act
        rule.MarkTriggered();

        // Assert
        rule.LastTriggeredAt.Should().NotBeNull();
        rule.LastTriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        rule.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkTriggered_CalledTwice_ShouldUpdateToLatestTime()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");

        // Act
        rule.MarkTriggered();
        var firstTriggeredAt = rule.LastTriggeredAt;
        rule.MarkTriggered();

        // Assert
        rule.LastTriggeredAt.Should().BeOnOrAfter(firstTriggeredAt!.Value);
    }

    [Fact]
    public void MarkTriggered_ShouldNotIncrementVersion()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");

        // Act
        rule.MarkTriggered();

        // Assert
        rule.Version.Should().Be(0);
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        rule.IsDeleted.Should().BeFalse();

        // Act
        rule.SoftDelete();

        // Assert
        rule.IsDeleted.Should().BeTrue();
        rule.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SoftDelete_ShouldIncrementVersion()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");

        // Act
        rule.SoftDelete();

        // Assert
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void SoftDelete_AfterUpdate_ShouldHaveCorrectVersion()
    {
        // Arrange
        var rule = new AlertRule("user-1", "Test", "PriceAlert",
            "Above", 100m, "InApp");
        rule.Update(name: "Updated");

        // Act
        rule.SoftDelete();

        // Assert
        rule.Version.Should().Be(2);
        rule.IsDeleted.Should().BeTrue();
    }

    #endregion
}
