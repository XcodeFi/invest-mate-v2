using FluentAssertions;
using Moq;
using InvestmentApp.Application.CapitalFlows.Queries.GetFlowHistory;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.CapitalFlows.Queries;

public class GetFlowHistoryQueryHandlerTests
{
    private readonly Mock<ICapitalFlowRepository> _flowRepo;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly GetFlowHistoryQueryHandler _handler;

    public GetFlowHistoryQueryHandlerTests()
    {
        _flowRepo = new Mock<ICapitalFlowRepository>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _handler = new GetFlowHistoryQueryHandler(_flowRepo.Object, _portfolioRepo.Object);
    }

    [Fact]
    public async Task Handle_PortfolioWithSeedAndUserFlows_SeedInListButExcludedFromAggregates()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Main", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var seed = new CapitalFlow(portfolio.Id, "user1", CapitalFlowType.Deposit, 100_000_000m, isSeedDeposit: true);
        var userDeposit = new CapitalFlow(portfolio.Id, "user1", CapitalFlowType.Deposit, 30_000_000m);
        var userDividend = new CapitalFlow(portfolio.Id, "user1", CapitalFlowType.Dividend, 5_000_000m);

        _flowRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { seed, userDeposit, userDividend });

        var query = new GetFlowHistoryQuery { PortfolioId = portfolio.Id, UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — Flows list keeps seed (audit trail)
        result.Flows.Should().HaveCount(3);
        result.Flows.Should().Contain(f => f.IsSeedDeposit);

        // Aggregates exclude seed — only user activity counts
        result.TotalDeposits.Should().Be(30_000_000m); // NOT 130M
        result.TotalDividends.Should().Be(5_000_000m);
        result.NetCashFlow.Should().Be(35_000_000m); // NOT 135M
    }
}
