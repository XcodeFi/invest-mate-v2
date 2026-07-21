using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Trades.Commands.BulkCreateTrades;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Trades.Commands;

public class BulkCreateTradesCommandHandlerTests
{
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly BulkCreateTradesCommandHandler _handler;

    public BulkCreateTradesCommandHandlerTests()
    {
        _handler = new BulkCreateTradesCommandHandler(_tradeRepo.Object, _portfolioRepo.Object);
    }

    private (Portfolio portfolio, BulkCreateTradesCommand command) Setup()
    {
        var portfolio = new Portfolio("user1", "Test Portfolio", 10_000_000m);
        var command = new BulkCreateTradesCommand
        {
            UserId = portfolio.UserId,
            PortfolioId = portfolio.Id,
            Trades = new()
            {
                new BulkTradeItem { Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 80_000m, Fee = 15_000m, Tax = 0m }
            }
        };
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        return (portfolio, command);
    }

    [Fact]
    public async Task Handle_PortfolioOwnedByAnotherUser_ThrowsUnauthorized()
    {
        var (_, command) = Setup();
        command.UserId = "someone-else";

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_OwnedPortfolio_CreatesTrades()
    {
        var (_, command) = Setup();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
    }
}
