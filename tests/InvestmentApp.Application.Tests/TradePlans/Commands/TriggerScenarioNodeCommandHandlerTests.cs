using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.TriggerScenarioNode;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

public class TriggerScenarioNodeCommandHandlerTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo;
    private readonly TriggerScenarioNodeCommandHandler _handler;

    public TriggerScenarioNodeCommandHandlerTests()
    {
        _tradePlanRepo = new Mock<ITradePlanRepository>();
        _handler = new TriggerScenarioNodeCommandHandler(_tradePlanRepo.Object);
    }

    private static TradePlan CreatePlanWithScenarios(string userId = "user-1")
    {
        var plan = new TradePlan(userId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "SL hòa vốn",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.MoveStopToBreakeven
            }
        });
        return plan;
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldTriggerNodeAndSave()
    {
        var plan = CreatePlanWithScenarios();
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new TriggerScenarioNodeCommand
        {
            PlanId = plan.Id,
            UserId = "user-1",
            NodeId = "root-1",
            TradeId = "trade-1"
        };

        await _handler.Handle(command, CancellationToken.None);

        var node = plan.ScenarioNodes!.First(n => n.NodeId == "root-1");
        node.Status.Should().Be(ScenarioNodeStatus.Triggered);
        _tradePlanRepo.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PlanNotFound_ShouldThrow()
    {
        _tradePlanRepo.Setup(r => r.GetByIdAsync("invalid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradePlan?)null);

        var command = new TriggerScenarioNodeCommand
        {
            PlanId = "invalid",
            UserId = "user-1",
            NodeId = "root-1"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_WrongUser_ShouldThrow()
    {
        var plan = CreatePlanWithScenarios("user-1");
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new TriggerScenarioNodeCommand
        {
            PlanId = plan.Id,
            UserId = "wrong-user",
            NodeId = "root-1"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
