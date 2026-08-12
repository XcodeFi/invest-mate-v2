using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// TradePlan.SetScenarioNodes đã có luật "Simple thì không nhận node". Vế lọc theo chế độ ở
/// handler khiến luật đó không bao giờ chạy tới: nodes rơi im lặng mà tool vẫn trả "ok".
/// </summary>
public class ScenarioNodeModeGuardTests
{
    private static TradePlan SimplePlan()
        => new("u1", "ANV", "Buy", 20000m, 18000m, 26000m, 800);

    private static ScenarioNodeDto Node() => new()
    {
        NodeId = "n1",
        Order = 1,
        Label = "Chốt 1/2",
        ConditionType = ScenarioConditionType.PriceAbove,
        ConditionValue = 24000m,
        ActionType = ScenarioActionType.SellPercent,
        ActionValue = 50m
    };

    private static (UpdateTradePlanCommandHandler handler, TradePlan plan) Build(bool advanced)
    {
        var plan = SimplePlan();
        if (advanced) plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return (new UpdateTradePlanCommandHandler(repo.Object, Mock.Of<ICompanyDossierGate>()), plan);
    }

    private static UpdateTradePlanCommand Cmd() => new()
    {
        Id = "p1",
        UserId = "u1",
        ScenarioNodes = new List<ScenarioNodeDto> { Node() }
    };

    [Fact]
    public async Task Sending_Nodes_To_Simple_Plan_Throws_Instead_Of_Dropping_Silently()
    {
        var (handler, _) = Build(advanced: false);

        var act = () => handler.Handle(Cmd(), default);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .And.Message.Should().Contain("Simple");
    }

    [Fact]
    public async Task Plan_Already_Advanced_Still_Accepts_Nodes_Alone()
    {
        var (handler, plan) = Build(advanced: true);

        await handler.Handle(Cmd(), default);

        plan.ScenarioNodes.Should().HaveCount(1);
        plan.ScenarioNodes[0].ActionType.Should().Be(ScenarioActionType.SellPercent);
    }
}
