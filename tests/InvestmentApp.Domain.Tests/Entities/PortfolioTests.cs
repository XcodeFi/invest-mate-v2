using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Events;

namespace InvestmentApp.Domain.Tests.Entities;

public class PortfolioTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreatePortfolio()
    {
        // Arrange
        var userId = "user-1";
        var name = "My Portfolio";
        var initialCapital = 100_000_000m;

        // Act
        var portfolio = new Portfolio(userId, name, initialCapital);

        // Assert
        portfolio.Id.Should().NotBeNullOrEmpty();
        portfolio.UserId.Should().Be(userId);
        portfolio.Name.Should().Be(name);
        portfolio.InitialCapital.Should().Be(initialCapital);
        portfolio.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        portfolio.IsDeleted.Should().BeFalse();
        portfolio.Trades.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ZeroInitialCapital_ShouldCreatePortfolio()
    {
        // Act
        var portfolio = new Portfolio("user-1", "Test", 0m);

        // Assert
        portfolio.InitialCapital.Should().Be(0m);
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Portfolio(null!, "Name", 100m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Portfolio("user-1", null!, 100m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_NegativeInitialCapital_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Portfolio("user-1", "Name", -1m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("initialCapital");
    }

    #endregion

    #region UpdateName

    [Fact]
    public void UpdateName_ValidName_ShouldUpdateName()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Old Name", 100m);

        // Act
        portfolio.UpdateName("New Name");

        // Assert
        portfolio.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateName_NullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        var action = () => portfolio.UpdateName(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    #endregion

    #region UpdateInitialCapital

    [Fact]
    public void UpdateInitialCapital_ValidAmount_ShouldUpdateCapital()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        portfolio.UpdateInitialCapital(500_000_000m);

        // Assert
        portfolio.InitialCapital.Should().Be(500_000_000m);
    }

    [Fact]
    public void UpdateInitialCapital_Zero_ShouldUpdateCapital()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        portfolio.UpdateInitialCapital(0m);

        // Assert
        portfolio.InitialCapital.Should().Be(0m);
    }

    [Fact]
    public void UpdateInitialCapital_NegativeAmount_ShouldThrowArgumentException()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        var action = () => portfolio.UpdateInitialCapital(-1m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("initialCapital");
    }

    #endregion

    #region MarkAsDeleted

    [Fact]
    public void MarkAsDeleted_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        portfolio.MarkAsDeleted();

        // Assert
        portfolio.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void MarkAsDeleted_CalledTwice_ShouldRemainDeleted()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        portfolio.MarkAsDeleted();
        portfolio.MarkAsDeleted();

        // Assert
        portfolio.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region AddTrade

    [Fact]
    public void AddTrade_ValidTrade_ShouldAddTradeAndRaiseDomainEvent()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 85000m);

        // Act
        portfolio.AddTrade(trade);

        // Assert
        portfolio.Trades.Should().HaveCount(1);
        portfolio.Trades.Should().Contain(trade);
        portfolio.DomainEvents.Should().HaveCount(1);
        portfolio.DomainEvents.First().Should().BeOfType<TradeCreatedEvent>();

        var domainEvent = (TradeCreatedEvent)portfolio.DomainEvents.First();
        domainEvent.TradeId.Should().Be(trade.Id);
        domainEvent.PortfolioId.Should().Be(portfolio.Id);
        domainEvent.Symbol.Should().Be("VNM");
        domainEvent.Quantity.Should().Be(100);
        domainEvent.Price.Should().Be(85000m);
    }

    [Fact]
    public void AddTrade_ValidTrade_ShouldIncrementVersion()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var initialVersion = portfolio.Version;
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 85000m);

        // Act
        portfolio.AddTrade(trade);

        // Assert
        portfolio.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void AddTrade_NullTrade_ShouldThrowArgumentNullException()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        var action = () => portfolio.AddTrade(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("trade");
    }

    [Fact]
    public void AddTrade_WrongPortfolioId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var trade = new Trade("wrong-portfolio-id", "VNM", TradeType.BUY, 100, 85000m);

        // Act
        var action = () => portfolio.AddTrade(trade);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not belong*");
    }

    [Fact]
    public void AddTrade_MultipleTrades_ShouldAddAllAndRaiseMultipleEvents()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var trade1 = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 85000m);
        var trade2 = new Trade(portfolio.Id, "FPT", TradeType.SELL, 50, 120000m);

        // Act
        portfolio.AddTrade(trade1);
        portfolio.AddTrade(trade2);

        // Assert
        portfolio.Trades.Should().HaveCount(2);
        portfolio.DomainEvents.Should().HaveCount(2);
        portfolio.DomainEvents.Should().AllBeOfType<TradeCreatedEvent>();
    }

    #endregion

    #region RemoveTrade

    [Fact]
    public void RemoveTrade_ExistingTrade_ShouldRemoveTradeAndRaiseDomainEvent()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 85000m);
        portfolio.AddTrade(trade);
        portfolio.ClearDomainEvents();

        // Act
        portfolio.RemoveTrade(trade.Id);

        // Assert
        portfolio.Trades.Should().BeEmpty();
        portfolio.DomainEvents.Should().HaveCount(1);
        portfolio.DomainEvents.First().Should().BeOfType<TradeDeletedEvent>();

        var domainEvent = (TradeDeletedEvent)portfolio.DomainEvents.First();
        domainEvent.TradeId.Should().Be(trade.Id);
        domainEvent.PortfolioId.Should().Be(portfolio.Id);
    }

    [Fact]
    public void RemoveTrade_ExistingTrade_ShouldIncrementVersion()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 85000m);
        portfolio.AddTrade(trade);
        var versionAfterAdd = portfolio.Version;

        // Act
        portfolio.RemoveTrade(trade.Id);

        // Assert
        portfolio.Version.Should().Be(versionAfterAdd + 1);
    }

    [Fact]
    public void RemoveTrade_NonExistentTradeId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var portfolio = new Portfolio("user-1", "Name", 100m);

        // Act
        var action = () => portfolio.RemoveTrade("non-existent-id");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion
}
