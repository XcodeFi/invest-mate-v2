using FluentAssertions;
using InvestmentApp.Application.Alerts.Queries.GetAlertHistory;
using InvestmentApp.Application.Alerts.Queries.GetAlertRules;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.Portfolios.Queries.GetPnL;
using InvestmentApp.Application.TradePlans.Queries.GetExecutedPlansForReview;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;
using InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

/// <summary>
/// Kiểm chủ sở hữu của các query đợt 3 MCP (kế hoạch, lãi/lỗ, cảnh báo) trước khi bọc thành tool.
/// Đối tượng không có và đối tượng của người khác cho CÙNG một loại lỗi và CÙNG thông điệp; dịch vụ giữ dữ liệu không bao giờ được gọi.
/// </summary>
public class PlansPnlAlertsOwnershipTests
{
    private const string Me = "user-1";
    private const string Other = "user-2";

    private static TradePlan PlanOf(string userId) => new(userId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
        thesis: "Mua mẫu cho test kiểm chủ sở hữu — luận điểm đủ dài tối thiểu");

    // ---------- preview_plan_review ----------

    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICampaignReviewService> _review = new();

    private PreviewPlanReviewQueryHandler PreviewHandler() =>
        new(_plans.Object, _trades.Object, _portfolios.Object, _review.Object);

    [Fact]
    public async Task PreviewReview_missing_and_foreign_plans_give_the_same_exception_type_and_message()
    {
        var foreign = PlanOf(Other);
        _plans.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        _plans.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((TradePlan?)null);

        Exception? foreignEx = null, missingEx = null;
        try { await PreviewHandler().Handle(new PreviewPlanReviewQuery { PlanId = foreign.Id, UserId = Me }, default); }
        catch (Exception e) { foreignEx = e; }
        try { await PreviewHandler().Handle(new PreviewPlanReviewQuery { PlanId = "khong-co", UserId = Me }, default); }
        catch (Exception e) { missingEx = e; }

        foreignEx.Should().BeOfType<KeyNotFoundException>();
        missingEx.Should().BeOfType<KeyNotFoundException>();
        foreignEx!.Message.Should().Be($"Trade plan {foreign.Id} not found");
        missingEx!.Message.Should().Be("Trade plan khong-co not found");
        // cùng khuôn: chỉ khác id người gọi đã biết
        foreignEx.Message.Replace(foreign.Id, "X").Should().Be(missingEx.Message.Replace("khong-co", "X"));
    }

    [Fact]
    public async Task PreviewReview_foreign_plan_never_reaches_trades_or_the_review_service()
    {
        var foreign = PlanOf(Other);
        _plans.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var act = () => PreviewHandler().Handle(new PreviewPlanReviewQuery { PlanId = foreign.Id, UserId = Me }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _trades.VerifyNoOtherCalls();
        _review.VerifyNoOtherCalls();
    }

    // ---------- get_plan_scenario_history ----------

    [Fact]
    public async Task ScenarioHistory_foreign_plan_is_refused_and_alert_history_is_never_read()
    {
        var foreign = PlanOf(Other);
        var alerts = new Mock<IAlertHistoryRepository>();
        _plans.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var act = () => new GetScenarioHistoryQueryHandler(_plans.Object, alerts.Object)
            .Handle(new GetScenarioHistoryQuery { TradePlanId = foreign.Id, UserId = Me }, default);

        (await act.Should().ThrowAsync<KeyNotFoundException>()).Which.Message.Should().Be($"Trade plan {foreign.Id} not found");
        alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScenarioHistory_missing_plan_gives_the_same_refusal()
    {
        var alerts = new Mock<IAlertHistoryRepository>();
        _plans.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((TradePlan?)null);

        var act = () => new GetScenarioHistoryQueryHandler(_plans.Object, alerts.Object)
            .Handle(new GetScenarioHistoryQuery { TradePlanId = "khong-co", UserId = Me }, default);

        (await act.Should().ThrowAsync<KeyNotFoundException>()).Which.Message.Should().Be("Trade plan khong-co not found");
        alerts.VerifyNoOtherCalls();
    }

    // ---------- các query lọc theo người gọi: chỉ hỏi kho bằng id của người gọi ----------

    [Fact]
    public async Task ScenarioTemplates_ask_the_repository_for_the_caller_only_and_return_only_their_templates()
    {
        var repo = new Mock<IScenarioTemplateRepository>();
        repo.Setup(r => r.GetByUserIdAsync(Me)).ReturnsAsync(new List<ScenarioTemplate>
        {
            new() { Id = "t-me", UserId = Me, Name = "Của tôi", Nodes = new List<ScenarioNode>() }
        });
        repo.Setup(r => r.GetByUserIdAsync(Other)).ReturnsAsync(new List<ScenarioTemplate>
        {
            new() { Id = "t-other", UserId = Other, Name = "Của người khác", Nodes = new List<ScenarioNode>() }
        });

        var result = await new GetScenarioTemplatesQueryHandler(repo.Object)
            .Handle(new GetScenarioTemplatesQuery { UserId = Me }, default);

        result.Select(t => t.Id).Should().Contain("t-me").And.NotContain("t-other");
        repo.Verify(r => r.GetByUserIdAsync(Other), Times.Never);
    }

    [Fact]
    public async Task PendingReview_asks_for_the_caller_only()
    {
        _plans.Setup(r => r.GetExecutedByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradePlan>());

        await new GetExecutedPlansForReviewQueryHandler(_plans.Object)
            .Handle(new GetExecutedPlansForReviewQuery { UserId = Me }, default);

        _plans.Verify(r => r.GetExecutedByUserIdAsync(Me, It.IsAny<CancellationToken>()), Times.Once);
        _plans.Verify(r => r.GetExecutedByUserIdAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
        _plans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AlertRules_ask_for_the_caller_only()
    {
        var repo = new Mock<IAlertRuleRepository>();
        repo.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<AlertRule>());

        await new GetAlertRulesQueryHandler(repo.Object).Handle(new GetAlertRulesQuery { UserId = Me }, default);

        repo.Verify(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlertHistory_asks_for_the_caller_only_in_both_modes(bool unreadOnly)
    {
        var repo = new Mock<IAlertHistoryRepository>();
        repo.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<AlertHistory>());
        repo.Setup(r => r.GetUnreadByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<AlertHistory>());
        repo.Setup(r => r.GetUnreadCountAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await new GetAlertHistoryQueryHandler(repo.Object)
            .Handle(new GetAlertHistoryQuery { UserId = Me, UnreadOnly = unreadOnly }, default);

        repo.Verify(r => r.GetByUserIdAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.GetUnreadByUserIdAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.GetUnreadCountAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- PnL: kiểm chủ trong handler, IPnLService không tự kiểm ----------

    private readonly Mock<IPnLService> _pnl = new();
    private readonly Mock<ICapitalFlowRepository> _flows = new();

    private Portfolio Mine(string name = "Danh mục của tôi", decimal capital = 100_000_000m)
    {
        var p = new Portfolio(Me, name, capital);
        _portfolios.Setup(r => r.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        return p;
    }

    private Portfolio Foreign()
    {
        var p = new Portfolio(Other, "Danh mục người khác", 100_000_000m);
        _portfolios.Setup(r => r.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        return p;
    }

    private void Missing(string id) =>
        _portfolios.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Portfolio?)null);

    [Fact]
    public async Task PortfolioPnL_foreign_and_missing_give_the_same_message_and_the_service_is_never_called()
    {
        var foreign = Foreign();
        Missing("khong-co");
        var handler = new GetPortfolioPnLQueryHandler(_portfolios.Object, _pnl.Object);

        var f = await FluentActions.Awaiting(() => handler.Handle(new GetPortfolioPnLQuery { PortfolioId = foreign.Id, UserId = Me }, default))
            .Should().ThrowAsync<KeyNotFoundException>();
        var m = await FluentActions.Awaiting(() => handler.Handle(new GetPortfolioPnLQuery { PortfolioId = "khong-co", UserId = Me }, default))
            .Should().ThrowAsync<KeyNotFoundException>();

        f.Which.Message.Replace(foreign.Id, "X").Should().Be(m.Which.Message.Replace("khong-co", "X"));
        _pnl.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PortfolioPnL_own_portfolio_returns_the_same_figures_as_the_rest_endpoint()
    {
        var mine = Mine();
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(mine.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new PortfolioPnLSummary
        {
            TotalInvested = 1_000m, TotalPortfolioValue = 1_200m, TotalRealizedPnL = 50m, TotalUnrealizedPnL = 150m,
            Positions = new List<PositionPnL> { new() { Symbol = "VNM", Quantity = 10, AverageCost = 100, CurrentPrice = 120 } }
        });

        var r = await new GetPortfolioPnLQueryHandler(_portfolios.Object, _pnl.Object)
            .Handle(new GetPortfolioPnLQuery { PortfolioId = mine.Id, UserId = Me }, default);

        r.PortfolioId.Should().Be(mine.Id);
        r.PortfolioName.Should().Be("Danh mục của tôi");
        r.TotalInvested.Should().Be(1_000m);
        r.TotalMarketValue.Should().Be(1_200m);
        r.TotalPnL.Should().Be(200m);
        r.TotalPnLPercent.Should().Be(20m);
        r.Positions.Should().ContainSingle(p => p.Symbol == "VNM");
    }

    [Fact]
    public async Task PositionPnL_foreign_and_missing_give_the_same_message_and_the_service_is_never_called()
    {
        var foreign = Foreign();
        Missing("khong-co");
        var handler = new GetPositionPnLQueryHandler(_portfolios.Object, _pnl.Object);

        var f = await FluentActions.Awaiting(() => handler.Handle(new GetPositionPnLQuery { PortfolioId = foreign.Id, Symbol = "VNM", UserId = Me }, default))
            .Should().ThrowAsync<KeyNotFoundException>();
        var m = await FluentActions.Awaiting(() => handler.Handle(new GetPositionPnLQuery { PortfolioId = "khong-co", Symbol = "VNM", UserId = Me }, default))
            .Should().ThrowAsync<KeyNotFoundException>();

        f.Which.Message.Replace(foreign.Id, "X").Should().Be(m.Which.Message.Replace("khong-co", "X"));
        _pnl.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PositionPnL_own_portfolio_passes_the_normalized_symbol_and_keeps_the_services_own_errors()
    {
        var mine = Mine();
        _pnl.Setup(s => s.CalculatePositionPnLAsync(mine.Id, It.Is<StockSymbol>(x => x.Value == "VNM"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionPnL { Symbol = "VNM", Quantity = 5 });
        _pnl.Setup(s => s.CalculatePositionPnLAsync(mine.Id, It.Is<StockSymbol>(x => x.Value == "ZZZ"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("No trades found for symbol ZZZ"));
        var handler = new GetPositionPnLQueryHandler(_portfolios.Object, _pnl.Object);

        (await handler.Handle(new GetPositionPnLQuery { PortfolioId = mine.Id, Symbol = " vnm ", UserId = Me }, default))
            .Symbol.Should().Be("VNM");
        await handler.Invoking(h => h.Handle(new GetPositionPnLQuery { PortfolioId = mine.Id, Symbol = "ZZZ", UserId = Me }, default))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*No trades*");
    }

    [Fact]
    public async Task OverallPnL_reads_only_the_callers_portfolios_and_never_a_foreign_one()
    {
        var a = Mine("A", 100m);
        _portfolios.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { a });
        _flows.Setup(f => f.GetTotalFlowByPortfolioIdAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new PortfolioPnLSummary());

        await new GetOverallPnLQueryHandler(_portfolios.Object, _flows.Object, _pnl.Object)
            .Handle(new GetOverallPnLQuery { UserId = Me }, default);

        _portfolios.Verify(r => r.GetByUserIdAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
        _pnl.Verify(s => s.CalculatePortfolioPnLAsync(It.Is<string>(id => id != a.Id), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OverallPnL_matches_the_rest_semantics_net_flow_for_all_failing_portfolios_skipped()
    {
        var a = Mine("A", 100m);
        var b = Mine("B (chưa có giao dịch)", 50m);
        _portfolios.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { a, b });
        _flows.Setup(f => f.GetTotalFlowByPortfolioIdAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(10m);
        _flows.Setup(f => f.GetTotalFlowByPortfolioIdAsync(b.Id, It.IsAny<CancellationToken>())).ReturnsAsync(5m);
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new PortfolioPnLSummary
        {
            TotalInvested = 100m, TotalPortfolioValue = 130m, TotalRealizedPnL = 10m, TotalUnrealizedPnL = 20m
        });
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(b.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("No trades"));

        var r = await new GetOverallPnLQueryHandler(_portfolios.Object, _flows.Object, _pnl.Object)
            .Handle(new GetOverallPnLQuery { UserId = Me }, default);

        r.TotalPortfolios.Should().Be(2);
        r.TotalInitialCapital.Should().Be(150m);
        r.TotalNetCashFlow.Should().Be(15m, "dòng tiền của danh mục chưa có giao dịch vẫn được cộng");
        r.TotalCurrentCapital.Should().Be(165m);
        r.TotalInvested.Should().Be(100m);
        r.TotalMarketValue.Should().Be(130m);
        r.TotalPnL.Should().Be(30m);
        r.TotalPnLPercent.Should().Be(30m);
        r.Portfolios.Should().ContainSingle().Which.PortfolioId.Should().Be(a.Id);
        r.Portfolios[0].CurrentCapital.Should().Be(110m);
    }
}
