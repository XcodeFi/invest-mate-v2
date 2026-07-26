using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Commands.DeletePortfolio;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Portfolios.Commands;

public class DeletePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly DeletePortfolioCommandHandler _handler;

    public DeletePortfolioCommandHandlerTests()
    {
        _handler = new DeletePortfolioCommandHandler(_portfolioRepo.Object, _audit.Object);
    }

    [Fact]
    public async Task Handle_Owner_ReturnsTrue_AndDeletes()
    {
        var portfolio = new Portfolio("user1", "Danh mục", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var result = await _handler.Handle(
            new DeletePortfolioCommand { Id = portfolio.Id, UserId = "user1" }, CancellationToken.None);

        result.Should().BeTrue();
        _portfolioRepo.Verify(r => r.DeleteAsync(portfolio.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherUsersPortfolio_ReturnsFalse_AndDoesNotDelete()
    {
        var portfolio = new Portfolio("owner", "Danh mục", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);

        var result = await _handler.Handle(
            new DeletePortfolioCommand { Id = portfolio.Id, UserId = "attacker" }, CancellationToken.None);

        result.Should().BeFalse();
        _portfolioRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsFalse()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Portfolio?)null);

        var result = await _handler.Handle(
            new DeletePortfolioCommand { Id = "missing", UserId = "user1" }, CancellationToken.None);

        result.Should().BeFalse();
        _portfolioRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
