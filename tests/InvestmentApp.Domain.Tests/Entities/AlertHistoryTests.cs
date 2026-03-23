using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class AlertHistoryTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_RequiredParametersOnly_ShouldCreateAlertHistory()
    {
        // Arrange
        var userId = "user-1";
        var alertRuleId = "rule-1";
        var alertType = "PriceAlert";
        var title = "VNM reached target price";
        var message = "VNM hit 85,000 VND";

        // Act
        var alert = new AlertHistory(userId, alertRuleId, alertType, title, message);

        // Assert
        alert.Id.Should().NotBeNullOrEmpty();
        alert.UserId.Should().Be(userId);
        alert.AlertRuleId.Should().Be(alertRuleId);
        alert.AlertType.Should().Be(alertType);
        alert.Title.Should().Be(title);
        alert.Message.Should().Be(message);
        alert.PortfolioId.Should().BeNull();
        alert.Symbol.Should().BeNull();
        alert.CurrentValue.Should().BeNull();
        alert.ThresholdValue.Should().BeNull();
        alert.IsRead.Should().BeFalse();
        alert.TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_AllParameters_ShouldSetOptionalFields()
    {
        // Arrange
        var userId = "user-1";
        var alertRuleId = "rule-1";
        var alertType = "PriceAlert";
        var title = "VNM reached target";
        var message = "VNM hit threshold";
        var portfolioId = "portfolio-1";
        var symbol = "VNM";
        var currentValue = 85000m;
        var thresholdValue = 84000m;

        // Act
        var alert = new AlertHistory(userId, alertRuleId, alertType, title, message,
            portfolioId, symbol, currentValue, thresholdValue);

        // Assert
        alert.PortfolioId.Should().Be(portfolioId);
        alert.Symbol.Should().Be(symbol);
        alert.CurrentValue.Should().Be(currentValue);
        alert.ThresholdValue.Should().Be(thresholdValue);
    }

    [Fact]
    public void Constructor_NullMessage_ShouldDefaultToEmpty()
    {
        // Act
        var alert = new AlertHistory("user-1", "rule-1", "PriceAlert", "Title", null!);

        // Assert
        alert.Message.Should().BeEmpty();
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertHistory(null!, "rule-1", "PriceAlert", "Title", "Msg");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullAlertRuleId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertHistory("user-1", null!, "PriceAlert", "Title", "Msg");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("alertRuleId");
    }

    [Fact]
    public void Constructor_NullAlertType_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertHistory("user-1", "rule-1", null!, "Title", "Msg");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("alertType");
    }

    [Fact]
    public void Constructor_NullTitle_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new AlertHistory("user-1", "rule-1", "PriceAlert", null!, "Msg");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("title");
    }

    #endregion

    #region Constructor — Defaults

    [Fact]
    public void Constructor_IsRead_ShouldDefaultToFalse()
    {
        // Act
        var alert = new AlertHistory("user-1", "rule-1", "PriceAlert", "Title", "Msg");

        // Assert
        alert.IsRead.Should().BeFalse();
    }

    [Fact]
    public void Constructor_TriggeredAt_ShouldBeCloseToUtcNow()
    {
        // Act
        var alert = new AlertHistory("user-1", "rule-1", "PriceAlert", "Title", "Msg");

        // Assert
        alert.TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region MarkAsRead

    [Fact]
    public void MarkAsRead_ShouldSetIsReadToTrue()
    {
        // Arrange
        var alert = new AlertHistory("user-1", "rule-1", "PriceAlert", "Title", "Msg");
        alert.IsRead.Should().BeFalse();

        // Act
        alert.MarkAsRead();

        // Assert
        alert.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRead_CalledTwice_ShouldRemainTrue()
    {
        // Arrange
        var alert = new AlertHistory("user-1", "rule-1", "PriceAlert", "Title", "Msg");

        // Act
        alert.MarkAsRead();
        alert.MarkAsRead();

        // Assert
        alert.IsRead.Should().BeTrue();
    }

    #endregion
}
