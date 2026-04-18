using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Portfolios.Queries;

public class GetPortfolioQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<ICapitalFlowRepository> _flowRepo;
    private readonly GetPortfolioQueryHandler _handler;

    public GetPortfolioQueryHandlerTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _flowRepo = new Mock<ICapitalFlowRepository>();
        _handler = new GetPortfolioQueryHandler(
            _portfolioRepo.Object,
            _tradeRepo.Object,
            _flowRepo.Object);
    }

    [Fact]
    public async Task Handle_PortfolioWithFlows_ReturnsCurrentCapitalIncludingNetFlows()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Main", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15_000_000m);

        var query = new GetPortfolioQuery { Id = portfolio.Id, UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.InitialCapital.Should().Be(100_000_000m);
        result.NetCashFlow.Should().Be(15_000_000m);
        result.CurrentCapital.Should().Be(115_000_000m);
    }

    [Fact]
    public async Task Handle_WrongUserId_ReturnsNull()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Main", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var query = new GetPortfolioQuery { Id = portfolio.Id, UserId = "otherUser" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ReturnsNull()
    {
        // Arrange
        _portfolioRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var query = new GetPortfolioQuery { Id = "missing", UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _flowRepo.Verify(r => r.GetTotalFlowByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
