using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Trades.Commands.LinkTradeToPlan;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Trades.Commands;

/// <summary>
/// Two entities are loaded by reference (trade → portfolio, and plan), so BOTH need an
/// owner check — otherwise a crafted planId splices another user's plan onto your trade.
/// </summary>
public class LinkTradeToPlanCommandHandlerTests
{
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly LinkTradeToPlanCommandHandler _handler;

    public LinkTradeToPlanCommandHandlerTests()
    {
        _handler = new LinkTradeToPlanCommandHandler(_tradeRepo.Object, _planRepo.Object, _portfolioRepo.Object);
    }

    private Trade SetupTradeOwnedBy(string ownerId)
    {
        var portfolio = new Portfolio(ownerId, "Danh mục", 100_000_000m);
        var trade = new Trade(portfolio.Id, "FPT", TradeType.BUY, 100, 120_000m, 0m, 0m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        return trade;
    }

    private TradePlan SetupPlanOwnedBy(string ownerId, bool inProgress = false)
    {
        var plan = new TradePlan(ownerId, "FPT", "Buy", 120_000m, 110_000m, 140_000m, 100,
            thesis: "Thesis đủ dài để qua gate kỷ luật của plan nhỏ.");
        if (inProgress)
        {
            plan.MarkReady();
            plan.MarkInProgress();
        }
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    [Fact]
    public async Task Handle_OwnerLinksOwnTradeAndInProgressPlan_ReturnsTrue()
    {
        var trade = SetupTradeOwnedBy("user1");
        var plan = SetupPlanOwnedBy("user1", inProgress: true);

        var result = await _handler.Handle(
            new LinkTradeToPlanCommand { TradeId = trade.Id, PlanId = plan.Id, UserId = "user1" },
            CancellationToken.None);

        result.Should().BeTrue();
        _tradeRepo.Verify(r => r.UpdateAsync(trade, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Documents a sharp edge now exposed to MCP agents: an unlinked plan that is not
    /// InProgress makes the handler call plan.Execute(), which throws rather than
    /// returning false. The tool description warns about it.
    /// </summary>
    [Fact]
    public async Task Handle_PlanNotInProgress_Throws()
    {
        var trade = SetupTradeOwnedBy("user1");
        var plan = SetupPlanOwnedBy("user1");   // Draft

        var act = () => _handler.Handle(
            new LinkTradeToPlanCommand { TradeId = trade.Id, PlanId = plan.Id, UserId = "user1" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*in-progress*");
    }

    [Fact]
    public async Task Handle_OtherUsersTrade_ReturnsFalse_AndDoesNotUpdate()
    {
        var trade = SetupTradeOwnedBy("owner");
        var plan = SetupPlanOwnedBy("attacker");

        var result = await _handler.Handle(
            new LinkTradeToPlanCommand { TradeId = trade.Id, PlanId = plan.Id, UserId = "attacker" },
            CancellationToken.None);

        result.Should().BeFalse();
        _tradeRepo.Verify(r => r.UpdateAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
        _planRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OtherUsersPlan_ReturnsFalse_AndDoesNotUpdate()
    {
        var trade = SetupTradeOwnedBy("user1");
        var plan = SetupPlanOwnedBy("someone-else");

        var result = await _handler.Handle(
            new LinkTradeToPlanCommand { TradeId = trade.Id, PlanId = plan.Id, UserId = "user1" },
            CancellationToken.None);

        result.Should().BeFalse();
        _tradeRepo.Verify(r => r.UpdateAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
        _planRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
