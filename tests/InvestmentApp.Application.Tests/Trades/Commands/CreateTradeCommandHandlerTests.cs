using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Trades.Commands;

public class CreateTradeCommandHandlerTests
{
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly CreateTradeCommandHandler _handler;

    public CreateTradeCommandHandlerTests()
    {
        _tradeRepo = new Mock<ITradeRepository>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new CreateTradeCommandHandler(
            _tradeRepo.Object,
            _portfolioRepo.Object,
            _auditService.Object);
    }

    /// <summary>
    /// Creates a real Portfolio and a command whose PortfolioId matches portfolio.Id,
    /// so that Portfolio.AddTrade(trade) validation passes (trade.PortfolioId == portfolio.Id).
    /// </summary>
    private (Portfolio portfolio, CreateTradeCommand command) CreatePortfolioAndCommand(
        string tradeType = "BUY")
    {
        var portfolio = new Portfolio("user1", "Test Portfolio", 10_000_000m);

        var command = new CreateTradeCommand
        {
            PortfolioId = portfolio.Id,
            Symbol = "VNM",
            TradeType = tradeType,
            Quantity = 100,
            Price = 80_000m,
            Fee = 15_000m,
            Tax = 0m
        };

        _portfolioRepo
            .Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        return (portfolio, command);
    }

    [Fact]
    public async Task Handle_ValidBuyTrade_CreatesTradeAndReturnsId()
    {
        // Arrange
        var (portfolio, command) = CreatePortfolioAndCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();

        _tradeRepo.Verify(
            r => r.AddAsync(
                It.Is<Trade>(t =>
                    t.PortfolioId == portfolio.Id &&
                    t.Symbol == "VNM" &&
                    t.TradeType == TradeType.BUY &&
                    t.Quantity == 100 &&
                    t.Price == 80_000m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new CreateTradeCommand
        {
            PortfolioId = "nonexistent",
            Symbol = "VNM",
            TradeType = "BUY",
            Quantity = 100,
            Price = 80_000m
        };
        _portfolioRepo
            .Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Portfolio not found*");
    }

    [Fact]
    public async Task Handle_InvalidTradeType_ThrowsArgumentException()
    {
        // Arrange
        var (_, command) = CreatePortfolioAndCommand(tradeType: "INVALID_TYPE");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid trade type*");
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsTradeToPortfolio()
    {
        // Arrange
        var (portfolio, command) = CreatePortfolioAndCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _portfolioRepo.Verify(
            r => r.UpdateAsync(
                It.Is<Portfolio>(p => p.Trades.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsAudit()
    {
        // Arrange
        var (_, command) = CreatePortfolioAndCommand();

        AuditEntry? capturedAudit = null;
        _auditService
            .Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => capturedAudit = entry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedAudit.Should().NotBeNull();
        capturedAudit!.Action.Should().Be("CREATE_TRADE");
        capturedAudit.EntityId.Should().Be(result);
        capturedAudit.UserId.Should().Be("user1");
    }
}
