using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Trades.Commands.DeleteTrade;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Trades.Commands;

/// <summary>
/// Ownership is transitive here (trade → portfolio), so the cross-user rejection path
/// needs its own guard — a crafted tradeId must never delete another user's trade.
/// </summary>
public class DeleteTradeCommandHandlerTests
{
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly DeleteTradeCommandHandler _handler;

    public DeleteTradeCommandHandlerTests()
    {
        _handler = new DeleteTradeCommandHandler(_tradeRepo.Object, _portfolioRepo.Object, _audit.Object);
    }

    private (Trade trade, Portfolio portfolio) SetupOwnedBy(string ownerId)
    {
        var portfolio = new Portfolio(ownerId, "Danh mục", 100_000_000m);
        var trade = new Trade(portfolio.Id, "FPT", TradeType.BUY, 100, 120_000m, 0m, 0m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        return (trade, portfolio);
    }

    [Fact]
    public async Task Handle_OwnerDeletes_ReturnsTrue_AndDeletes()
    {
        var (trade, _) = SetupOwnedBy("user1");

        var result = await _handler.Handle(
            new DeleteTradeCommand { TradeId = trade.Id, UserId = "user1" }, CancellationToken.None);

        result.Should().BeTrue();
        _tradeRepo.Verify(r => r.DeleteAsync(trade.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherUsersTrade_ReturnsFalse_AndDoesNotDelete()
    {
        var (trade, _) = SetupOwnedBy("owner");

        var result = await _handler.Handle(
            new DeleteTradeCommand { TradeId = trade.Id, UserId = "attacker" }, CancellationToken.None);

        result.Should().BeFalse();
        _tradeRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TradeNotFound_ReturnsFalse()
    {
        _tradeRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Trade?)null);

        var result = await _handler.Handle(
            new DeleteTradeCommand { TradeId = "missing", UserId = "user1" }, CancellationToken.None);

        result.Should().BeFalse();
        _tradeRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
