using FluentAssertions;
using InvestmentApp.Application.Alerts.Commands.CreateAlertRule;
using InvestmentApp.Application.Alerts.Commands.UpdateAlertRule;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.DailyRoutines.Commands.GetOrCreateTodayRoutine;
using InvestmentApp.Application.DailyRoutines.Commands.SwitchTemplate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Moq;

namespace InvestmentApp.Application.Tests.Common;

/// <summary>
/// Id liên kết nhận từ request (danh mục, chiến lược, giao dịch, kế hoạch, mẫu routine) chỉ được
/// lưu khi thuộc tài khoản đang gọi; id null vẫn hợp lệ. Ca âm dùng thực thể THẬT của người
/// khác và khẳng định ngoại lệ + không ghi gì; mỗi handler có ca đối chứng dùng thực thể của mình.
/// </summary>
public class LinkIdOwnershipTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IStrategyRepository> _strategyRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<ITradePlanRepository> _planRepo = new();

    private readonly Portfolio _myPortfolio = new(Me, "Của tôi", 100_000_000m);
    private readonly Portfolio _foreignPortfolio = new(Other, "Của người khác", 100_000_000m);
    private readonly Strategy _myStrategy = new(Me, "Của tôi", "d", "e", "x", "r", "D1", "Trending");
    private readonly Strategy _foreignStrategy = new(Other, "Của người khác", "d", "e", "x", "r", "D1", "Trending");

    public LinkIdOwnershipTests()
    {
        foreach (var p in new[] { _myPortfolio, _foreignPortfolio })
            _portfolioRepo.Setup(r => r.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        foreach (var s in new[] { _myStrategy, _foreignStrategy })
            _strategyRepo.Setup(r => r.GetByIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);
    }

    private TradePlan AddPlan(string userId)
    {
        var plan = new TradePlan(userId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
            thesis: "Luận điểm đủ dài để qua cổng kỷ luật");
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private Trade AddTrade(Portfolio portfolio)
    {
        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 80_000m);
        _tradeRepo.Setup(r => r.GetByIdAsync(trade.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        return trade;
    }

    // ---------- CreateTradePlan ----------

    private CreateTradePlanCommandHandler CreatePlanHandler() =>
        new(_planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object, _strategyRepo.Object,
            Mock.Of<ICompanyDossierGate>());

    private static CreateTradePlanCommand CreatePlanCmd(string? portfolioId, string? strategyId) => new()
    {
        UserId = Me, Symbol = "VNM", Direction = "Buy",
        EntryPrice = 80_000m, StopLoss = 75_000m, Target = 90_000m, Quantity = 100,
        MarketCondition = "Trending", ConfidenceLevel = 5,
        PortfolioId = portfolioId, StrategyId = strategyId
    };

    [Fact]
    public async Task CreateTradePlan_ForeignPortfolio_Throws_NothingSaved()
    {
        var act = () => CreatePlanHandler().Handle(CreatePlanCmd(_foreignPortfolio.Id, null), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _planRepo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTradePlan_ForeignStrategy_Throws_NothingSaved()
    {
        var act = () => CreatePlanHandler().Handle(CreatePlanCmd(null, _foreignStrategy.Id), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _planRepo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTradePlan_OwnLinks_And_NullLinks_Succeed()
    {
        await CreatePlanHandler().Handle(CreatePlanCmd(_myPortfolio.Id, _myStrategy.Id), default);
        await CreatePlanHandler().Handle(CreatePlanCmd(null, null), default);

        _planRepo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ---------- UpdateTradePlan ----------

    private UpdateTradePlanCommandHandler UpdatePlanHandler() =>
        new(_planRepo.Object, _portfolioRepo.Object, _strategyRepo.Object, Mock.Of<ICompanyDossierGate>());

    [Fact]
    public async Task UpdateTradePlan_ForeignPortfolio_Throws_PlanUntouched()
    {
        var plan = AddPlan(Me);

        var act = () => UpdatePlanHandler().Handle(
            new UpdateTradePlanCommand { Id = plan.Id, UserId = Me, PortfolioId = _foreignPortfolio.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        plan.PortfolioId.Should().BeNull();
        _planRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTradePlan_ForeignStrategy_Throws_PlanUntouched()
    {
        var plan = AddPlan(Me);

        var act = () => UpdatePlanHandler().Handle(
            new UpdateTradePlanCommand { Id = plan.Id, UserId = Me, StrategyId = _foreignStrategy.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        plan.StrategyId.Should().BeNull();
        _planRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTradePlan_OwnLinks_Succeed()
    {
        var plan = AddPlan(Me);

        await UpdatePlanHandler().Handle(
            new UpdateTradePlanCommand
            {
                Id = plan.Id, UserId = Me, PortfolioId = _myPortfolio.Id, StrategyId = _myStrategy.Id
            }, default);

        plan.PortfolioId.Should().Be(_myPortfolio.Id);
        plan.StrategyId.Should().Be(_myStrategy.Id);
    }

    // ---------- CreateJournalEntry ----------

    private CreateJournalEntryCommandHandler JournalHandler()
    {
        var market = new Mock<IMarketDataProvider>();
        return new CreateJournalEntryCommandHandler(
            Mock.Of<IJournalEntryRepository>(), _portfolioRepo.Object, _tradeRepo.Object, _planRepo.Object,
            market.Object, Mock.Of<IAuditService>());
    }

    private static CreateJournalEntryCommand JournalCmd(string? portfolioId = null, string? tradeId = null, string? planId = null) => new()
    {
        UserId = Me, Symbol = "VNM", EntryType = "Observation", Title = "t", Content = "c",
        PortfolioId = portfolioId, TradeId = tradeId, TradePlanId = planId, PriceAtTime = 80_000m
    };

    [Fact]
    public async Task CreateJournalEntry_ForeignPortfolio_Throws()
    {
        var act = () => JournalHandler().Handle(JournalCmd(portfolioId: _foreignPortfolio.Id), default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateJournalEntry_ForeignTrade_Throws()
    {
        var trade = AddTrade(_foreignPortfolio);
        var act = () => JournalHandler().Handle(JournalCmd(tradeId: trade.Id), default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateJournalEntry_ForeignTradePlan_Throws()
    {
        var plan = AddPlan(Other);
        var act = () => JournalHandler().Handle(JournalCmd(planId: plan.Id), default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateJournalEntry_OwnLinks_And_NullLinks_Succeed()
    {
        var trade = AddTrade(_myPortfolio);
        var plan = AddPlan(Me);

        var id1 = await JournalHandler().Handle(JournalCmd(_myPortfolio.Id, trade.Id, plan.Id), default);
        var id2 = await JournalHandler().Handle(JournalCmd(), default);

        id1.Should().NotBeNullOrEmpty();
        id2.Should().NotBeNullOrEmpty();
    }

    // ---------- Alert rules ----------

    private readonly Mock<IAlertRuleRepository> _alertRepo = new();

    [Fact]
    public async Task CreateAlertRule_ForeignPortfolio_Throws_NothingSaved()
    {
        var handler = new CreateAlertRuleCommandHandler(_alertRepo.Object, _portfolioRepo.Object);

        var act = () => handler.Handle(new CreateAlertRuleCommand
        {
            UserId = Me, Name = "n", AlertType = "PortfolioValue", Threshold = 1m, PortfolioId = _foreignPortfolio.Id
        }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _alertRepo.Verify(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAlertRule_OwnPortfolio_And_NoPortfolio_Succeed()
    {
        var handler = new CreateAlertRuleCommandHandler(_alertRepo.Object, _portfolioRepo.Object);

        await handler.Handle(new CreateAlertRuleCommand
        {
            UserId = Me, Name = "n", AlertType = "PortfolioValue", Threshold = 1m, PortfolioId = _myPortfolio.Id
        }, default);
        await handler.Handle(new CreateAlertRuleCommand
        {
            UserId = Me, Name = "n", AlertType = "PriceAlert", Threshold = 1m, Symbol = "VNM"
        }, default);

        _alertRepo.Verify(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private AlertRule AddRule()
    {
        var rule = new AlertRule(Me, "n", "PortfolioValue", "Above", 1m, "InApp");
        _alertRepo.Setup(r => r.GetByIdAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        return rule;
    }

    [Fact]
    public async Task UpdateAlertRule_ForeignPortfolio_Throws_RuleUntouched()
    {
        var rule = AddRule();
        var handler = new UpdateAlertRuleCommandHandler(_alertRepo.Object, _portfolioRepo.Object);

        var act = () => handler.Handle(
            new UpdateAlertRuleCommand { Id = rule.Id, UserId = Me, PortfolioId = _foreignPortfolio.Id }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        rule.PortfolioId.Should().BeNull();
        _alertRepo.Verify(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAlertRule_OwnPortfolio_Succeeds()
    {
        var rule = AddRule();
        var handler = new UpdateAlertRuleCommandHandler(_alertRepo.Object, _portfolioRepo.Object);

        await handler.Handle(
            new UpdateAlertRuleCommand { Id = rule.Id, UserId = Me, PortfolioId = _myPortfolio.Id }, default);

        rule.PortfolioId.Should().Be(_myPortfolio.Id);
    }

    // ---------- Routine template ----------

    private readonly Mock<IDailyRoutineRepository> _routineRepo = new();
    private readonly Mock<IRoutineTemplateRepository> _templateRepo = new();

    private RoutineTemplate AddTemplate(string? ownerId)
    {
        var t = new RoutineTemplate(ownerId, "Mẫu", "📋", "swing", 10, new List<RoutineItemTemplate>());
        _templateRepo.Setup(r => r.GetByIdAsync(t.Id, It.IsAny<CancellationToken>())).ReturnsAsync(t);
        return t;
    }

    private GetOrCreateTodayRoutineCommandHandler TodayHandler() => new(_routineRepo.Object, _templateRepo.Object);
    private SwitchTemplateCommandHandler SwitchHandler() => new(_routineRepo.Object, _templateRepo.Object);

    [Fact]
    public async Task GetOrCreateToday_ForeignCustomTemplate_Throws_NothingSaved()
    {
        var t = AddTemplate(Other);

        var act = () => TodayHandler().Handle(
            new GetOrCreateTodayRoutineCommand { UserId = Me, TemplateId = t.Id, LocalDate = "2026-11-24" }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _routineRepo.Verify(r => r.AddAsync(It.IsAny<DailyRoutine>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Me)]
    public async Task GetOrCreateToday_BuiltInOrOwnTemplate_Succeeds(string? owner)
    {
        var t = AddTemplate(owner);

        await TodayHandler().Handle(
            new GetOrCreateTodayRoutineCommand { UserId = Me, TemplateId = t.Id, LocalDate = "2026-11-24" }, default);

        _routineRepo.Verify(r => r.AddAsync(It.IsAny<DailyRoutine>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SwitchTemplate_ForeignCustomTemplate_Throws_NothingSaved()
    {
        var t = AddTemplate(Other);

        var act = () => SwitchHandler().Handle(
            new SwitchTemplateCommand { UserId = Me, TemplateId = t.Id, LocalDate = "2026-11-24" }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _routineRepo.Verify(r => r.AddAsync(It.IsAny<DailyRoutine>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.UpdateAsync(It.IsAny<DailyRoutine>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Me)]
    public async Task SwitchTemplate_BuiltInOrOwnTemplate_Succeeds(string? owner)
    {
        var t = AddTemplate(owner);

        await SwitchHandler().Handle(
            new SwitchTemplateCommand { UserId = Me, TemplateId = t.Id, LocalDate = "2026-11-24" }, default);

        _routineRepo.Verify(r => r.AddAsync(It.IsAny<DailyRoutine>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
