using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Risk;

public class GetVolatilitySizingForPlanQueryHandlerTests
{
    private readonly Mock<IVolatilityBudgetService> _service = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();

    private GetVolatilitySizingForPlanQueryHandler CreateHandler() =>
        new(_service.Object, _portfolioRepo.Object);

    private static GetVolatilitySizingForPlanQuery Query(string userId = "owner") => new()
    {
        PortfolioId = "port-1", UserId = userId, Symbol = "FPT", EntryPrice = 100_000m, Quantity = 100
    };

    private static Portfolio PortfolioOf(string userId)
    {
        var portfolio = new Portfolio(userId, "Danh mục chính", 1_000_000_000m);
        typeof(Portfolio).GetProperty(nameof(Portfolio.Id))!.SetValue(portfolio, "port-1");
        return portfolio;
    }

    [Fact]
    public async Task PortfolioOfAnotherUser_Throws()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf("someone-else"));

        var act = async () => await CreateHandler().Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        _service.Verify(s => s.GetSizingForPlanAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never, "không được chạm tới dữ liệu danh mục của người khác");
    }

    [Fact]
    public async Task PortfolioNotFound_Throws()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var act = async () => await CreateHandler().Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OwnedPortfolio_PassesArgumentsThrough()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf("owner"));
        _service.Setup(s => s.GetSizingForPlanAsync("port-1", "FPT", 100_000m, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VolatilitySizingResult { Symbol = "FPT", MaxQuantityWithinBudget = 560 });

        var result = await CreateHandler().Handle(Query(), CancellationToken.None);

        result.MaxQuantityWithinBudget.Should().Be(560);
        _service.Verify(s => s.GetSizingForPlanAsync("port-1", "FPT", 100_000m, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
