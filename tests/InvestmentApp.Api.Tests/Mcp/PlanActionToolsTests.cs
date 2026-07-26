using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
using InvestmentApp.Application.Risk.Commands.SetRiskProfile;
using InvestmentApp.Application.Risk.Commands.SetStopLossTarget;
using InvestmentApp.Application.TradePlans.Commands.AbortTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>P4 — plan execution & discipline actions. All write tools (Destructive).</summary>
public class PlanActionToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ResolveDecision_ExecuteSell_SetsUserIdAndAction()
    {
        McpTestContext.Capture<ResolveDecisionResult, ResolveDecisionCommand>(
            _mediator, out var sent, new ResolveDecisionResult());
        await PlanActionTools.ResolveDecision("StopLossHit:p1:FPT", "ExecuteSell",
            _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None, tradePlanId: "plan-1");
        sent()!.UserId.Should().Be("u-1");
        sent()!.DecisionId.Should().Be("StopLossHit:p1:FPT");
        sent()!.Action.Should().Be(DecisionAction.ExecuteSell);
        sent()!.TradePlanId.Should().Be("plan-1");
    }

    [Fact]
    public async Task ResolveDecision_HoldWithJournal_PassesNoteAndSymbol()
    {
        McpTestContext.Capture<ResolveDecisionResult, ResolveDecisionCommand>(
            _mediator, out var sent, new ResolveDecisionResult());
        await PlanActionTools.ResolveDecision("d1", "HoldWithJournal",
            _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None,
            symbol: "fpt", note: "Giữ vì thesis dài hạn vẫn còn nguyên vẹn.");
        sent()!.Action.Should().Be(DecisionAction.HoldWithJournal);
        sent()!.Symbol.Should().Be("FPT");
        sent()!.Note.Should().StartWith("Giữ vì");
    }

    [Fact]
    public async Task ResolveDecision_RejectsUnknownAction()
    {
        var act = () => PlanActionTools.ResolveDecision("d1", "SellEverything",
            _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReviewTradePlan_SetsUserIdPlanIdAndLessons()
    {
        McpTestContext.Capture<CampaignReviewDto, ReviewTradePlanCommand>(
            _mediator, out var sent, new CampaignReviewDto());
        await PlanActionTools.ReviewTradePlan("plan-2", _mediator.Object,
            McpTestContext.WithUser("u-4"), CancellationToken.None, lessonsLearned: "Vào lệnh sớm quá.");
        sent()!.UserId.Should().Be("u-4");
        sent()!.PlanId.Should().Be("plan-2");
        sent()!.LessonsLearned.Should().Be("Vào lệnh sớm quá.");
    }

    [Fact]
    public async Task AbortTradePlan_SetsTriggerAndDetail()
    {
        McpTestContext.Capture<AbortTradePlanResult, AbortTradePlanCommand>(
            _mediator, out var sent, new AbortTradePlanResult());
        await PlanActionTools.AbortTradePlan("plan-3", "EarningsMiss",
            "KQKD quý 2 thấp hơn kỳ vọng 30%, luận điểm tăng trưởng không còn đúng.",
            _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-5");
        sent()!.PlanId.Should().Be("plan-3");
        sent()!.Trigger.Should().Be("EarningsMiss");
        sent()!.Detail.Should().StartWith("KQKD");
    }

    [Fact]
    public async Task AbortTradePlan_RejectsUnknownTrigger()
    {
        var act = () => PlanActionTools.AbortTradePlan("plan-3", "RandomReason",
            "Chi tiết đủ dài để qua ràng buộc hai mươi ký tự.",
            _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteLot_SetsPlanIdLotNumberTradeIdAndPrice()
    {
        McpTestContext.Capture<Unit, ExecuteLotCommand>(_mediator, out var sent, Unit.Value);
        await PlanActionTools.ExecuteLot("plan-4", 2, "trade-1", 121_500m,
            _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-7");
        sent()!.PlanId.Should().Be("plan-4");
        sent()!.LotNumber.Should().Be(2);
        sent()!.TradeId.Should().Be("trade-1");
        sent()!.ActualPrice.Should().Be(121_500m);
    }

    [Fact]
    public async Task UpdateStopLoss_SetsNewStopLossAndReason()
    {
        McpTestContext.Capture<Unit, UpdateStopLossCommand>(_mediator, out var sent, Unit.Value);
        await PlanActionTools.UpdateStopLoss("plan-5", 115_000m, _mediator.Object,
            McpTestContext.WithUser("u-8"), CancellationToken.None, reason: "Dời SL lên hòa vốn.");
        sent()!.UserId.Should().Be("u-8");
        sent()!.PlanId.Should().Be("plan-5");
        sent()!.NewStopLoss.Should().Be(115_000m);
        sent()!.Reason.Should().Be("Dời SL lên hòa vốn.");
    }

    [Fact]
    public async Task UpdateStopLoss_RejectsNonPositivePrice()
    {
        var act = () => PlanActionTools.UpdateStopLoss("plan-5", 0m, _mediator.Object,
            McpTestContext.WithUser("u-8"), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TriggerExitTarget_SetsLevelAndTradeId()
    {
        McpTestContext.Capture<Unit, TriggerExitTargetCommand>(_mediator, out var sent, Unit.Value);
        await PlanActionTools.TriggerExitTarget("plan-6", 1, "trade-2",
            _mediator.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-9");
        sent()!.PlanId.Should().Be("plan-6");
        sent()!.Level.Should().Be(1);
        sent()!.TradeId.Should().Be("trade-2");
    }

    [Fact]
    public async Task SetStopLossTarget_SetsAllPriceFields()
    {
        McpTestContext.Capture<string, SetStopLossTargetCommand>(_mediator, out var sent, "slt-1");
        var id = await PlanActionTools.SetStopLossTarget("trade-3", "p1", "hpg",
            27_000m, 25_000m, 32_000m, _mediator.Object,
            McpTestContext.WithUser("u-10"), CancellationToken.None, trailingStopPercent: 7m);
        sent()!.UserId.Should().Be("u-10");
        sent()!.TradeId.Should().Be("trade-3");
        sent()!.PortfolioId.Should().Be("p1");
        sent()!.Symbol.Should().Be("HPG");
        sent()!.EntryPrice.Should().Be(27_000m);
        sent()!.StopLossPrice.Should().Be(25_000m);
        sent()!.TargetPrice.Should().Be(32_000m);
        sent()!.TrailingStopPercent.Should().Be(7m);
        id.Should().Be("slt-1");
    }

    [Fact]
    public async Task SetRiskProfile_PassesOnlySuppliedLimits()
    {
        McpTestContext.Capture<string, SetRiskProfileCommand>(_mediator, out var sent, "rp-1");
        await PlanActionTools.SetRiskProfile("p2", _mediator.Object,
            McpTestContext.WithUser("u-11"), CancellationToken.None,
            maxPositionSizePercent: 10m, maxDailyTrades: 3);
        sent()!.UserId.Should().Be("u-11");
        sent()!.PortfolioId.Should().Be("p2");
        sent()!.MaxPositionSizePercent.Should().Be(10m);
        sent()!.MaxDailyTrades.Should().Be(3);
        sent()!.MaxDrawdownAlertPercent.Should().BeNull("field bỏ trống phải giữ null, không ghi đè 0");
        sent()!.DailyLossLimitPercent.Should().BeNull();
    }
}
