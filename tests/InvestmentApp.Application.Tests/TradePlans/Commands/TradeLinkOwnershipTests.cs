using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Strategies.Commands.LinkTrade;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Application.TradePlans.Commands.TriggerScenarioNode;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// Liên kết giao dịch chỉ được chấp nhận khi giao dịch thuộc danh mục của tài khoản đang gọi.
/// Mỗi ca âm dùng một danh mục THẬT của người khác (không phải mock null) và khẳng định
/// ngoại lệ cụ thể + không có ghi nào xảy ra. Mỗi handler có một ca dương đối chứng.
/// </summary>
public class TradeLinkOwnershipTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IStrategyRepository> _strategyRepo = new();

    private readonly Portfolio _myPortfolio = new(Me, "Của tôi", 100_000_000m);
    private readonly Portfolio _foreignPortfolio = new(Other, "Của người khác", 100_000_000m);

    public TradeLinkOwnershipTests()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync(_myPortfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_myPortfolio);
        _portfolioRepo.Setup(r => r.GetByIdAsync(_foreignPortfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_foreignPortfolio);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _myPortfolio });
    }

    private Trade AddTrade(Portfolio portfolio)
    {
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 80_000m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        return trade;
    }

    private TradePlan AddPlan(Action<TradePlan>? configure = null)
    {
        var plan = new TradePlan(Me, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
            thesis: "Luận điểm đủ dài để qua cổng kỷ luật");
        configure?.Invoke(plan);
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private void VerifyNoWrites(Trade trade)
    {
        _tradeRepo.Verify(r => r.UpdateAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
        _planRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
        _planRepo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
        trade.TradePlanId.Should().BeNull();
        trade.StrategyId.Should().BeNull();
    }

    // ---------- CreateTradePlan ----------

    private CreateTradePlanCommandHandler CreateHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object, Mock.Of<ICompanyDossierGate>());

    private static CreateTradePlanCommand CreateCmd(string tradeId) => new()
    {
        UserId = Me, Symbol = "VNM", Direction = "Buy",
        EntryPrice = 80_000m, StopLoss = 75_000m, Target = 90_000m, Quantity = 100,
        MarketCondition = "Trending", ConfidenceLevel = 5, TradeId = tradeId
    };

    [Fact]
    public async Task CreateTradePlan_ForeignTrade_Throws_NoPlanSaved_NoTradeWrite()
    {
        var trade = AddTrade(_foreignPortfolio);

        var act = () => CreateHandler().Handle(CreateCmd(trade.Id), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
    }

    [Fact]
    public async Task CreateTradePlan_OwnTrade_LinksTrade()
    {
        var trade = AddTrade(_myPortfolio);
        TradePlan? saved = null;
        _planRepo.Setup(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()))
            .Callback<TradePlan, CancellationToken>((p, _) => saved = p);

        await CreateHandler().Handle(CreateCmd(trade.Id), default);

        saved.Should().NotBeNull();
        trade.TradePlanId.Should().Be(saved!.Id);
        _tradeRepo.Verify(r => r.UpdateAsync(trade, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- ExecuteLot ----------

    private TradePlan PlanWithLot() => AddPlan(p => p.SetLots(EntryMode.ScalingIn, new List<PlanLot>
    {
        new() { LotNumber = 1, PlannedPrice = 80_000m, PlannedQuantity = 100 }
    }));

    private ExecuteLotCommandHandler ExecuteLotHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);

    [Fact]
    public async Task ExecuteLot_ForeignTrade_Throws_PlanUntouched()
    {
        var trade = AddTrade(_foreignPortfolio);
        var plan = PlanWithLot();

        var act = () => ExecuteLotHandler().Handle(
            new ExecuteLotCommand { PlanId = plan.Id, UserId = Me, LotNumber = 1, TradeId = trade.Id, ActualPrice = 80_000m }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
        plan.TradeIds.Should().BeNullOrEmpty();
        plan.Lots![0].Status.Should().Be(PlanLotStatus.Pending);
    }

    [Fact]
    public async Task ExecuteLot_OwnTrade_LinksTrade()
    {
        var trade = AddTrade(_myPortfolio);
        var plan = PlanWithLot();

        await ExecuteLotHandler().Handle(
            new ExecuteLotCommand { PlanId = plan.Id, UserId = Me, LotNumber = 1, TradeId = trade.Id, ActualPrice = 80_000m }, default);

        trade.TradePlanId.Should().Be(plan.Id);
        plan.TradeIds.Should().Contain(trade.Id);
    }

    // ---------- TriggerExitTarget ----------

    private TradePlan PlanWithExitTarget() => AddPlan(p => p.SetExitTargets(new List<ExitTarget>
    {
        new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 90_000m }
    }));

    private TriggerExitTargetCommandHandler ExitTargetHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);

    [Fact]
    public async Task TriggerExitTarget_ForeignTrade_Throws_PlanUntouched()
    {
        var trade = AddTrade(_foreignPortfolio);
        var plan = PlanWithExitTarget();

        var act = () => ExitTargetHandler().Handle(
            new TriggerExitTargetCommand { PlanId = plan.Id, UserId = Me, Level = 1, TradeId = trade.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
        plan.TradeIds.Should().BeNullOrEmpty();
        plan.ExitTargets![0].IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task TriggerExitTarget_OwnTrade_LinksTrade()
    {
        var trade = AddTrade(_myPortfolio);
        var plan = PlanWithExitTarget();

        await ExitTargetHandler().Handle(
            new TriggerExitTargetCommand { PlanId = plan.Id, UserId = Me, Level = 1, TradeId = trade.Id }, default);

        trade.TradePlanId.Should().Be(plan.Id);
        plan.ExitTargets![0].IsTriggered.Should().BeTrue();
    }

    // ---------- UpdateTradePlanStatus ----------

    private UpdateTradePlanStatusCommandHandler StatusHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);

    [Fact]
    public async Task UpdateTradePlanStatus_ForeignTrade_Throws_PlanUntouched()
    {
        var trade = AddTrade(_foreignPortfolio);
        var plan = AddPlan();

        var act = () => StatusHandler().Handle(
            new UpdateTradePlanStatusCommand { Id = plan.Id, UserId = Me, Status = "executed", TradeId = trade.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
        plan.Status.Should().Be(TradePlanStatus.Draft);
        plan.TradeId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTradePlanStatus_OwnTrade_LinksTrade()
    {
        var trade = AddTrade(_myPortfolio);
        var plan = AddPlan();

        await StatusHandler().Handle(
            new UpdateTradePlanStatusCommand { Id = plan.Id, UserId = Me, Status = "executed", TradeId = trade.Id }, default);

        trade.TradePlanId.Should().Be(plan.Id);
        plan.Status.Should().Be(TradePlanStatus.Executed);
    }

    // ---------- TriggerScenarioNode ----------

    private TradePlan PlanWithNode() => AddPlan(p =>
    {
        p.SetExitStrategyMode(ExitStrategyMode.Advanced);
        p.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "n1", ParentId = null, Order = 0, Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove, ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent, ActionValue = 30m
            }
        });
    });

    private TriggerScenarioNodeCommandHandler NodeHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);

    [Fact]
    public async Task TriggerScenarioNode_ForeignTrade_Throws_PlanUntouched()
    {
        var trade = AddTrade(_foreignPortfolio);
        var plan = PlanWithNode();

        var act = () => NodeHandler().Handle(
            new TriggerScenarioNodeCommand { PlanId = plan.Id, UserId = Me, NodeId = "n1", TradeId = trade.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
        plan.TradeIds.Should().BeNullOrEmpty();
        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Pending);
    }

    [Fact]
    public async Task TriggerScenarioNode_OwnTrade_StoresTradeId()
    {
        var trade = AddTrade(_myPortfolio);
        var plan = PlanWithNode();

        await NodeHandler().Handle(
            new TriggerScenarioNodeCommand { PlanId = plan.Id, UserId = Me, NodeId = "n1", TradeId = trade.Id }, default);

        plan.TradeIds.Should().Contain(trade.Id);
        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Triggered);
    }

    [Fact]
    public async Task TriggerScenarioNode_NoTradeId_StillWorks()
    {
        var plan = PlanWithNode();

        await NodeHandler().Handle(
            new TriggerScenarioNodeCommand { PlanId = plan.Id, UserId = Me, NodeId = "n1" }, default);

        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Triggered);
    }

    // ---------- LinkTradeToStrategy ----------

    private Strategy AddStrategy()
    {
        var s = new Strategy(Me, "Chiến lược", "mô tả", "vào", "ra", "rủi ro", "D1", "Trending");
        _strategyRepo.Setup(r => r.GetByIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);
        return s;
    }

    private LinkTradeToStrategyCommandHandler StrategyHandler() =>
        new(_strategyRepo.Object, _tradeRepo.Object, _portfolioRepo.Object);

    [Fact]
    public async Task LinkTradeToStrategy_ForeignTrade_Throws_NoWrite()
    {
        var trade = AddTrade(_foreignPortfolio);
        var strategy = AddStrategy();

        var act = () => StrategyHandler().Handle(
            new LinkTradeToStrategyCommand { StrategyId = strategy.Id, TradeId = trade.Id, UserId = Me }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyNoWrites(trade);
    }

    [Fact]
    public async Task LinkTradeToStrategy_OwnTrade_Links()
    {
        var trade = AddTrade(_myPortfolio);
        var strategy = AddStrategy();

        await StrategyHandler().Handle(
            new LinkTradeToStrategyCommand { StrategyId = strategy.Id, TradeId = trade.Id, UserId = Me }, default);

        trade.StrategyId.Should().Be(strategy.Id);
    }

    // ---------- Đường đọc: review / preview chỉ tính giao dịch của người gọi ----------

    private TradePlan ExecutedPlanLinkedTo(Trade own, Trade foreign) => AddPlan(p =>
    {
        p.MarkReady();
        p.MarkInProgress();
        p.Execute(own.Id);
        p.SetExitTargets(new List<ExitTarget> { new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 90_000m } });
        p.TriggerExitTarget(1, foreign.Id);
    });

    private IReadOnlyList<Trade> CaptureTradesPassedToReview(Mock<ICampaignReviewService> service)
    {
        var captured = new List<Trade>();
        service.Setup(s => s.CalculateMetrics(It.IsAny<TradePlan>(), It.IsAny<IEnumerable<Trade>>()))
            .Callback<TradePlan, IEnumerable<Trade>>((_, t) => captured.AddRange(t))
            .Returns(new CampaignReviewData());
        return captured;
    }

    [Fact]
    public async Task ReviewTradePlan_IgnoresTradesFromOtherUsersPortfolios()
    {
        var own = AddTrade(_myPortfolio);
        var foreign = AddTrade(_foreignPortfolio);
        var plan = ExecutedPlanLinkedTo(own, foreign);
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { foreign });
        var service = new Mock<ICampaignReviewService>();
        var captured = CaptureTradesPassedToReview(service);
        var handler = new ReviewTradePlanCommandHandler(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object, service.Object);

        await handler.Handle(new ReviewTradePlanCommand { PlanId = plan.Id, UserId = Me }, default);

        captured.Select(t => t.Id).Should().BeEquivalentTo(new[] { own.Id });
    }

    [Fact]
    public async Task PreviewPlanReview_IgnoresTradesFromOtherUsersPortfolios()
    {
        var own = AddTrade(_myPortfolio);
        var foreign = AddTrade(_foreignPortfolio);
        var plan = ExecutedPlanLinkedTo(own, foreign);
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { foreign });
        var service = new Mock<ICampaignReviewService>();
        var captured = CaptureTradesPassedToReview(service);
        var handler = new PreviewPlanReviewQueryHandler(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object, service.Object);

        await handler.Handle(new PreviewPlanReviewQuery { PlanId = plan.Id, UserId = Me }, default);

        captured.Select(t => t.Id).Should().BeEquivalentTo(new[] { own.Id });
    }
}
