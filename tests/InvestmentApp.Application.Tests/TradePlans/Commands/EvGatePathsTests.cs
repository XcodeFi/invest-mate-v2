using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// Mọi đường người dùng/agent đưa kế hoạch Mua ra khỏi Draft (REST, AI agent, MCP đều đi qua ba lệnh này):
/// đường Ready tường minh đi qua cổng kỳ vọng (EvGateException → 400); đường ghi lại một lệnh đã khớp không bị chặn nhưng
/// được đánh dấu EvGateSkipped.
/// </summary>
public class EvGatePathsTests
{
    private const string Me = "u1";
    private const string Reason = "Chấp nhận kỳ vọng âm vì đây là lệnh thử nghiệm quy mô nhỏ";

    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private TradePlan? _saved;

    private static OutcomeScenarioDto S(string label, decimal p, decimal exit) =>
        new() { Label = label, ProbabilityPercent = p, ExitPrice = exit };

    private static List<OutcomeScenarioDto> Positive() => new() { S("Thắng", 60m, 120m), S("Thua", 40m, 90m) };
    private static List<OutcomeScenarioDto> Negative() => new() { S("Thắng", 20m, 120m), S("Thua", 80m, 90m) };

    private TradePlan DraftPlan(IEnumerable<OutcomeScenarioDto>? scenarios = null, bool withLots = false)
    {
        var plan = new TradePlan(Me, "VNM", "Buy", 100m, 90m, 130m, withLots ? 1 : 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        if (withLots)
            plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
            {
                new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
                new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
            });
        if (scenarios != null)
            plan.SetOutcomeScenarios(scenarios.Select(x => new OutcomeScenario
                { Label = x.Label, ProbabilityPercent = x.ProbabilityPercent, ExitPrice = x.ExitPrice }).ToList());
        _plans.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _plans.Setup(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()))
            .Callback<TradePlan, CancellationToken>((p, _) => _saved = p).Returns(Task.CompletedTask);
        return plan;
    }

    private Task Status(TradePlan plan, string status, string? reason = null, string? tradeId = null)
        => new UpdateTradePlanStatusCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
            .Handle(new UpdateTradePlanStatusCommand
                { Id = plan.Id, UserId = Me, Status = status, EvGateReason = reason, TradeId = tradeId }, default);

    private async Task<TradePlan> Create(CreateTradePlanCommand cmd)
    {
        TradePlan? saved = null;
        _plans.Setup(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()))
            .Callback<TradePlan, CancellationToken>((p, _) => saved = p);
        await new CreateTradePlanCommandHandler(_plans.Object, _trades.Object, _portfolios.Object,
            Mock.Of<IStrategyRepository>(), Mock.Of<ICompanyDossierGate>()).Handle(cmd, default);
        return saved!;
    }

    private static CreateTradePlanCommand Cmd(string? status = null, List<OutcomeScenarioDto>? scenarios = null, string? reason = null, string? tradeId = null)
        => new()
        {
            UserId = Me, Symbol = "VNM", Direction = "Buy", EntryPrice = 100m, StopLoss = 90m, Target = 130m, Quantity = 100,
            Thesis = "Luận điểm mẫu đủ dài cho test kỳ vọng R", Status = status, Scenarios = scenarios, EvGateReason = reason, TradeId = tradeId
        };

    // ---------- đường Ready tường minh: qua cổng ----------

    [Fact]
    public async Task status_ready_without_scenarios_and_without_reason_is_refused_with_the_gate_exception()
    {
        var plan = DraftPlan();

        var act = () => Status(plan, "ready");

        await act.Should().ThrowAsync<EvGateException>().WithMessage("*evGateReason*");
        plan.Status.Should().Be(TradePlanStatus.Draft);
        _plans.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task status_ready_with_negative_ev_and_no_reason_is_refused()
    {
        var plan = DraftPlan(Negative());

        var act = () => Status(plan, "ready");

        await act.Should().ThrowAsync<EvGateException>();
    }

    [Fact]
    public async Task status_ready_with_negative_ev_and_a_reason_goes_through_and_stores_the_reason()
    {
        var plan = DraftPlan(Negative());

        await Status(plan, "ready", Reason);

        plan.Status.Should().Be(TradePlanStatus.Ready);
        _saved!.EvGateReason.Should().Be(Reason);
        _saved.EvGateSkipped.Should().BeFalse();
    }

    [Fact]
    public async Task status_ready_with_positive_ev_needs_no_reason()
    {
        var plan = DraftPlan(Positive());

        await Status(plan, "ready");

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.Estimate.Should().NotBeNull();
    }

    [Fact]
    public async Task create_with_status_ready_and_no_scenarios_is_refused_and_nothing_is_saved()
    {
        var act = () => Create(Cmd(status: "Ready"));

        await act.Should().ThrowAsync<EvGateException>();
        _plans.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task create_with_status_ready_and_positive_scenarios_persists_them_and_the_estimate()
    {
        var plan = await Create(Cmd(status: "Ready", scenarios: Positive()));

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.OutcomeScenarios.Should().HaveCount(2);
        plan.Estimate!.ImpliedWinProbability.Should().Be(60m);
    }

    [Fact]
    public async Task create_with_status_ready_and_a_reason_stores_it()
    {
        var plan = await Create(Cmd(status: "Ready", scenarios: Negative(), reason: Reason));

        plan.EvGateReason.Should().Be(Reason);
    }

    [Fact]
    public async Task create_as_draft_with_scenarios_keeps_them_without_any_gate()
    {
        var plan = await Create(Cmd(scenarios: Negative()));

        plan.Status.Should().Be(TradePlanStatus.Draft);
        plan.OutcomeScenarios.Should().HaveCount(2);
        plan.Estimate.Should().BeNull("còn Draft thì chưa chụp");
    }

    // ---------- đường ghi lại lệnh đã khớp: không chặn, đánh dấu ----------

    [Fact]
    public async Task status_in_progress_from_draft_is_recorded_not_blocked()
    {
        var plan = DraftPlan();

        await Status(plan, "inprogress");

        plan.Status.Should().Be(TradePlanStatus.InProgress);
        _saved!.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task status_executed_from_draft_is_recorded_not_blocked()
    {
        var plan = DraftPlan();

        await Status(plan, "executed", tradeId: "t1");

        plan.Status.Should().Be(TradePlanStatus.Executed);
        _saved!.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task create_with_status_executed_is_recorded_not_blocked()
    {
        var plan = await Create(Cmd(status: "Executed", tradeId: "t1"));

        plan.Status.Should().Be(TradePlanStatus.Executed);
        plan.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task execute_lot_from_draft_is_recorded_not_blocked()
    {
        var plan = DraftPlan(withLots: true);

        await new ExecuteLotCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
            .Handle(new ExecuteLotCommand { PlanId = plan.Id, UserId = Me, LotNumber = 1, TradeId = "t1", ActualPrice = 100m }, default);

        plan.Status.Should().Be(TradePlanStatus.InProgress);
        _saved!.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task execute_lot_from_draft_with_positive_ev_is_not_flagged()
    {
        var plan = DraftPlan(Positive(), withLots: true);

        await new ExecuteLotCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
            .Handle(new ExecuteLotCommand { PlanId = plan.Id, UserId = Me, LotNumber = 1, TradeId = "t1", ActualPrice = 100m }, default);

        _saved!.EvGateSkipped.Should().BeFalse();
    }

    // ---------- sửa kịch bản, đọc lại ----------

    [Fact]
    public async Task update_with_scenarios_replaces_the_list_and_null_leaves_it_alone()
    {
        var plan = DraftPlan(Positive());
        var handler = new UpdateTradePlanCommandHandler(_plans.Object, _portfolios.Object,
            Mock.Of<IStrategyRepository>(), Mock.Of<ICompanyDossierGate>());

        await handler.Handle(new UpdateTradePlanCommand { Id = plan.Id, UserId = Me, Notes = "chỉ sửa ghi chú" }, default);
        plan.OutcomeScenarios.Should().HaveCount(2);
        plan.OutcomeScenarios![0].ProbabilityPercent.Should().Be(60m);

        await handler.Handle(new UpdateTradePlanCommand { Id = plan.Id, UserId = Me, Scenarios = Negative() }, default);
        plan.OutcomeScenarios![0].ProbabilityPercent.Should().Be(20m);

        await handler.Handle(new UpdateTradePlanCommand { Id = plan.Id, UserId = Me, Scenarios = new() }, default);
        plan.OutcomeScenarios.Should().BeNull("danh sách rỗng là xoá bảng");
    }

    [Fact]
    public async Task read_returns_scenarios_estimate_expectation_and_the_flags()
    {
        var plan = DraftPlan(Positive());
        plan.MarkReady();
        plan.MarkInProgress();
        _plans.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradePlan> { plan });

        var dto = (await new GetTradePlansQueryHandler(_plans.Object)
            .Handle(new GetTradePlansQuery { UserId = Me }, default)).Single();

        dto.Scenarios.Should().HaveCount(2);
        dto.Expectation!.EvR.Should().BeApproximately(0.8m, 0.0001m);
        dto.Expectation.ImpliedWinPercent.Should().Be(60m);
        dto.Estimate!.Locked.Should().BeTrue();
        dto.EvGateSkipped.Should().BeFalse();
        dto.EvGateReason.Should().BeNull();
    }

    [Fact]
    public async Task read_of_a_legacy_plan_without_scenarios_has_null_expectation_not_zero()
    {
        var plan = DraftPlan();
        _plans.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradePlan> { plan });

        var dto = (await new GetTradePlansQueryHandler(_plans.Object)
            .Handle(new GetTradePlansQuery { UserId = Me }, default)).Single();

        dto.Scenarios.Should().BeNull();
        dto.Expectation!.EvR.Should().BeNull();
        dto.Estimate.Should().BeNull();
    }
}
