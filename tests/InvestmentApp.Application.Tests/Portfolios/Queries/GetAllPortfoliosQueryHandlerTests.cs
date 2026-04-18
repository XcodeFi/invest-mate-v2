using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Portfolios.Queries;

public class GetAllPortfoliosQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<ICapitalFlowRepository> _flowRepo;
    private readonly GetAllPortfoliosQueryHandler _handler;

    public GetAllPortfoliosQueryHandlerTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _flowRepo = new Mock<ICapitalFlowRepository>();
        _handler = new GetAllPortfoliosQueryHandler(
            _portfolioRepo.Object,
            _tradeRepo.Object,
            _flowRepo.Object);
    }

    [Fact]
    public async Task Handle_PortfolioWithFlows_ReturnsCurrentCapitalIncludingNetFlows()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Main", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(20_000_000m);

        var query = new GetAllPortfoliosQuery { UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].InitialCapital.Should().Be(100_000_000m);
        result[0].NetCashFlow.Should().Be(20_000_000m);
        result[0].CurrentCapital.Should().Be(120_000_000m);
    }

    [Fact]
    public async Task Handle_PortfolioWithNoFlows_ReturnsCurrentCapitalEqualToInitial()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Empty", 50_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        // Assert
        result[0].NetCashFlow.Should().Be(0m);
        result[0].CurrentCapital.Should().Be(50_000_000m);
    }

    [Fact]
    public async Task Handle_PortfolioWithNetOutflow_ReturnsCurrentCapitalLessThanInitial()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Drained", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-30_000_000m);

        // Act
        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        // Assert
        result[0].NetCashFlow.Should().Be(-30_000_000m);
        result[0].CurrentCapital.Should().Be(70_000_000m);
    }
}
