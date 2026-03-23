using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class CapitalFlowTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateCapitalFlow()
    {
        // Arrange
        var portfolioId = "portfolio-1";
        var userId = "user-1";
        var type = CapitalFlowType.Deposit;
        var amount = 10_000_000m;

        // Act
        var flow = new CapitalFlow(portfolioId, userId, type, amount);

        // Assert
        flow.Id.Should().NotBeNullOrEmpty();
        flow.PortfolioId.Should().Be(portfolioId);
        flow.UserId.Should().Be(userId);
        flow.Type.Should().Be(type);
        flow.Amount.Should().Be(amount);
        flow.Currency.Should().Be("VND");
        flow.Note.Should().BeNull();
        flow.FlowDate.Should().BeCloseTo(DateTime.UtcNow.Date, TimeSpan.FromSeconds(1));
        flow.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldCreateCapitalFlow()
    {
        // Arrange
        var portfolioId = "portfolio-1";
        var userId = "user-1";
        var type = CapitalFlowType.Withdraw;
        var amount = 5_000_000m;
        var currency = "USD";
        var note = "Monthly withdrawal";
        var flowDate = new DateTime(2026, 1, 15);

        // Act
        var flow = new CapitalFlow(portfolioId, userId, type, amount, currency, note, flowDate);

        // Assert
        flow.PortfolioId.Should().Be(portfolioId);
        flow.UserId.Should().Be(userId);
        flow.Type.Should().Be(type);
        flow.Amount.Should().Be(amount);
        flow.Currency.Should().Be(currency);
        flow.Note.Should().Be(note);
        flow.FlowDate.Should().Be(flowDate.Date);
    }

    [Fact]
    public void Constructor_NullPortfolioId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new CapitalFlow(null!, "user-1", CapitalFlowType.Deposit, 100m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new CapitalFlow("portfolio-1", null!, CapitalFlowType.Deposit, 100m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_ZeroAmount_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 0m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Constructor_NegativeAmount_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, -500m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Constructor_DefaultCurrency_ShouldBeVND()
    {
        // Act
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 1000m);

        // Assert
        flow.Currency.Should().Be("VND");
    }

    [Fact]
    public void Constructor_NullCurrency_ShouldDefaultToVND()
    {
        // Act
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 1000m, null!);

        // Assert
        flow.Currency.Should().Be("VND");
    }

    [Fact]
    public void Constructor_CustomFlowDate_ShouldUseDatePartOnly()
    {
        // Arrange
        var flowDate = new DateTime(2026, 3, 15, 14, 30, 45);

        // Act
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 1000m, flowDate: flowDate);

        // Assert
        flow.FlowDate.Should().Be(new DateTime(2026, 3, 15));
        flow.FlowDate.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region UpdateNote

    [Fact]
    public void UpdateNote_WithValue_ShouldUpdateNote()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 1000m);

        // Act
        flow.UpdateNote("Updated note");

        // Assert
        flow.Note.Should().Be("Updated note");
    }

    [Fact]
    public void UpdateNote_WithNull_ShouldClearNote()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 1000m, note: "Initial note");

        // Act
        flow.UpdateNote(null);

        // Assert
        flow.Note.Should().BeNull();
    }

    #endregion

    #region SignedAmount

    [Fact]
    public void SignedAmount_Deposit_ShouldReturnPositiveAmount()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Deposit, 5_000_000m);

        // Act & Assert
        flow.SignedAmount.Should().Be(5_000_000m);
    }

    [Fact]
    public void SignedAmount_Dividend_ShouldReturnPositiveAmount()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Dividend, 500_000m);

        // Act & Assert
        flow.SignedAmount.Should().Be(500_000m);
    }

    [Fact]
    public void SignedAmount_Interest_ShouldReturnPositiveAmount()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Interest, 100_000m);

        // Act & Assert
        flow.SignedAmount.Should().Be(100_000m);
    }

    [Fact]
    public void SignedAmount_Withdraw_ShouldReturnNegativeAmount()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Withdraw, 3_000_000m);

        // Act & Assert
        flow.SignedAmount.Should().Be(-3_000_000m);
    }

    [Fact]
    public void SignedAmount_Fee_ShouldReturnNegativeAmount()
    {
        // Arrange
        var flow = new CapitalFlow("portfolio-1", "user-1", CapitalFlowType.Fee, 50_000m);

        // Act & Assert
        flow.SignedAmount.Should().Be(-50_000m);
    }

    #endregion
}
