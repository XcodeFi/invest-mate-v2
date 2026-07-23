using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Journals.Commands;

public class CreateJournalCommandHandlerTests
{
    private readonly Mock<ITradeJournalRepository> _journalRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly CreateJournalCommandHandler _handler;

    public CreateJournalCommandHandlerTests()
    {
        _handler = new CreateJournalCommandHandler(_journalRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);
    }

    private static Portfolio NewPortfolio(string id, string userId)
    {
        var p = new Portfolio(userId, "PF", 1_000_000m);
        typeof(Portfolio).GetProperty(nameof(Portfolio.Id))!.SetValue(p, id);
        return p;
    }

    [Fact]
    public async Task Handle_TradeBelongsToAnotherUser_ThrowsUnauthorized()
    {
        var trade = new Trade("pf-owner", "VNM", TradeType.BUY, 100, 50000);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync("pf-owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPortfolio("pf-owner", "owner-user"));

        var cmd = new CreateJournalCommand { TradeId = trade.Id, PortfolioId = "pf-attacker", UserId = "attacker-user" };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _journalRepo.Verify(r => r.AddAsync(It.IsAny<TradeJournal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TradeBelongsToCaller_CreatesJournal()
    {
        var trade = new Trade("pf-1", "VNM", TradeType.BUY, 100, 50000);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync("pf-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPortfolio("pf-1", "user-1"));
        _journalRepo.Setup(r => r.GetByTradeIdAsync(trade.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeJournal?)null);

        var cmd = new CreateJournalCommand { TradeId = trade.Id, PortfolioId = "pf-1", UserId = "user-1", EntryReason = "test" };
        var id = await _handler.Handle(cmd, CancellationToken.None);

        id.Should().NotBeNullOrEmpty();
        _journalRepo.Verify(r => r.AddAsync(It.IsAny<TradeJournal>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
