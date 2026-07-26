using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>Same transitive-ownership requirement as ExecuteLot — see that test's remarks.</summary>
public class TriggerExitTargetCommandHandlerTests
{
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly TriggerExitTargetCommandHandler _handler;

    public TriggerExitTargetCommandHandlerTests()
    {
        _handler = new TriggerExitTargetCommandHandler(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);
    }

    private TradePlan SetupPlanWithExitTarget(string ownerId)
    {
        var plan = new TradePlan(ownerId, "FPT", "Buy", 120_000m, 110_000m, 140_000m, 100,
            thesis: "Thesis đủ dài để qua gate kỷ luật của plan nhỏ.");
        plan.SetExitTargets(new List<ExitTarget>
        {
            new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 140_000m, Quantity = 50 }
        });
        plan.MarkReady();
        plan.MarkInProgress();
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private Trade SetupTradeOwnedBy(string ownerId)
    {
        var portfolio = new Portfolio(ownerId, "Danh mục", 100_000_000m);
        var trade = new Trade(portfolio.Id, "FPT", TradeType.SELL, 50, 140_000m, 0m, 0m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        return trade;
    }

    [Fact]
    public async Task Handle_OwnTradeAndPlan_LinksTrade()
    {
        var plan = SetupPlanWithExitTarget("user1");
        var trade = SetupTradeOwnedBy("user1");

        await _handler.Handle(new TriggerExitTargetCommand
        {
            PlanId = plan.Id, UserId = "user1", Level = 1, TradeId = trade.Id
        }, CancellationToken.None);

        _tradeRepo.Verify(r => r.UpdateAsync(trade, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherUsersTrade_Throws_AndDoesNotMutateTrade()
    {
        var plan = SetupPlanWithExitTarget("attacker");
        var victimTrade = SetupTradeOwnedBy("victim");

        var act = () => _handler.Handle(new TriggerExitTargetCommand
        {
            PlanId = plan.Id, UserId = "attacker", Level = 1, TradeId = victimTrade.Id
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tradeRepo.Verify(r => r.UpdateAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
        victimTrade.TradePlanId.Should().BeNull();
    }
}
