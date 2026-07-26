using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// The plan's owner is checked, but TradeId is caller-supplied and the trade gets mutated
/// (LinkTradePlan + UpdateAsync) — so the trade needs its own transitive owner check,
/// otherwise a crafted TradeId writes onto someone else's trade and drags it into this plan's P/L.
/// </summary>
public class ExecuteLotCommandHandlerTests
{
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly ExecuteLotCommandHandler _handler;

    public ExecuteLotCommandHandlerTests()
    {
        _handler = new ExecuteLotCommandHandler(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);
    }

    private TradePlan SetupMultiLotPlan(string ownerId)
    {
        var plan = new TradePlan(ownerId, "FPT", "Buy", 120_000m, 110_000m, 140_000m, 200,
            thesis: "Thesis đủ dài để qua gate kỷ luật của plan nhỏ.");
        plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
        {
            new() { LotNumber = 1, PlannedPrice = 120_000m, PlannedQuantity = 100 },
            new() { LotNumber = 2, PlannedPrice = 118_000m, PlannedQuantity = 100 }
        });
        plan.MarkReady();
        plan.MarkInProgress();
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private Trade SetupTradeOwnedBy(string ownerId)
    {
        var portfolio = new Portfolio(ownerId, "Danh mục", 100_000_000m);
        var trade = new Trade(portfolio.Id, "FPT", TradeType.BUY, 100, 120_000m, 0m, 0m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        return trade;
    }

    [Fact]
    public async Task Handle_OwnTradeAndPlan_LinksTrade()
    {
        var plan = SetupMultiLotPlan("user1");
        var trade = SetupTradeOwnedBy("user1");

        await _handler.Handle(new ExecuteLotCommand
        {
            PlanId = plan.Id, UserId = "user1", LotNumber = 1,
            TradeId = trade.Id, ActualPrice = 120_500m
        }, CancellationToken.None);

        _tradeRepo.Verify(r => r.UpdateAsync(trade, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherUsersTrade_Throws_AndDoesNotMutateTrade()
    {
        var plan = SetupMultiLotPlan("attacker");
        var victimTrade = SetupTradeOwnedBy("victim");

        var act = () => _handler.Handle(new ExecuteLotCommand
        {
            PlanId = plan.Id, UserId = "attacker", LotNumber = 1,
            TradeId = victimTrade.Id, ActualPrice = 120_500m
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tradeRepo.Verify(r => r.UpdateAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
        victimTrade.TradePlanId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OtherUsersPlan_Throws()
    {
        var plan = SetupMultiLotPlan("owner");

        var act = () => _handler.Handle(new ExecuteLotCommand
        {
            PlanId = plan.Id, UserId = "attacker", LotNumber = 1,
            TradeId = "whatever", ActualPrice = 120_500m
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
