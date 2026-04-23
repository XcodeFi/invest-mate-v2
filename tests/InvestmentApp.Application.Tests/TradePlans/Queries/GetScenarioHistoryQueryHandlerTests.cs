using System.Reflection;
using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

public class GetScenarioHistoryQueryHandlerTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo;
    private readonly Mock<IAlertHistoryRepository> _alertHistoryRepo;
    private readonly GetScenarioHistoryQueryHandler _handler;

    public GetScenarioHistoryQueryHandlerTests()
    {
        _tradePlanRepo = new Mock<ITradePlanRepository>();
        _alertHistoryRepo = new Mock<IAlertHistoryRepository>();
        _handler = new GetScenarioHistoryQueryHandler(_tradePlanRepo.Object, _alertHistoryRepo.Object);
    }

    private static TradePlan CreatePlanWithTriggeredNodes(string userId = "user-1")
    {
        var plan = new TradePlan(userId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
            thesis: "Mua mẫu cho test backward compat — luận điểm đủ dài tối thiểu");
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời 30%",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m,
                Status = ScenarioNodeStatus.Triggered,
                TriggeredAt = new DateTime(2026, 3, 15, 7, 30, 0, DateTimeKind.Utc)
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "SL hòa vốn",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.MoveStopToBreakeven,
                Status = ScenarioNodeStatus.Pending
            },
            new()
            {
                NodeId = "root-2",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ toàn bộ",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 75_000m,
                ActionType = ScenarioActionType.SellAll,
                Status = ScenarioNodeStatus.Pending
            }
        });
        return plan;
    }

    [Fact]
    public async Task Handle_ReturnsCorrectHistoryRecords_ForTriggeredAndPendingNodes()
    {
        // Arrange
        var plan = CreatePlanWithTriggeredNodes();

        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // AlertHistory for the triggered node — set TriggeredAt to match node's TriggeredAt
        var alertHistory = new AlertHistory(
            "user-1", plan.Id, "ScenarioPlaybook",
            "[VNM] Kịch bản: Chốt lời 30%",
            "Bán 30% vị thế. Giá hiện tại: 85,500đ",
            symbol: "VNM",
            currentValue: 85_500m,
            thresholdValue: 85_000m);
        typeof(AlertHistory).GetProperty("TriggeredAt")!
            .SetValue(alertHistory, new DateTime(2026, 3, 15, 7, 30, 0, DateTimeKind.Utc));

        _alertHistoryRepo.Setup(r => r.GetByAlertRuleIdAsync(plan.Id, "ScenarioPlaybook", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory> { alertHistory });

        var query = new GetScenarioHistoryQuery { TradePlanId = plan.Id, UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);

        var triggeredNode = result.First(r => r.NodeId == "root-1");
        triggeredNode.Label.Should().Be("Chốt lời 30%");
        triggeredNode.Status.Should().Be("Triggered");
        triggeredNode.TriggeredAt.Should().NotBeNull();
        triggeredNode.PriceAtTrigger.Should().Be(85_500m);
        triggeredNode.ActionType.Should().Be("SellPercent");
        triggeredNode.ActionValue.Should().Be(30m);

        var pendingNode = result.First(r => r.NodeId == "child-1");
        pendingNode.Status.Should().Be("Pending");
        pendingNode.PriceAtTrigger.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoScenarioNodes()
    {
        // Arrange
        var plan = new TradePlan("user-1", "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);
        // No scenario nodes set (Simple mode)

        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _alertHistoryRepo.Setup(r => r.GetByAlertRuleIdAsync(plan.Id, "ScenarioPlaybook", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());

        var query = new GetScenarioHistoryQuery { TradePlanId = plan.Id, UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsSortedByTriggeredAtDescending()
    {
        // Arrange
        var plan = new TradePlan("user-1", "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-a",
                ParentId = null,
                Order = 0,
                Label = "Trigger sớm",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 82_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 20m,
                Status = ScenarioNodeStatus.Triggered,
                TriggeredAt = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                NodeId = "node-b",
                ParentId = null,
                Order = 1,
                Label = "Trigger muộn",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m,
                Status = ScenarioNodeStatus.Triggered,
                TriggeredAt = new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc)
            },
            new()
            {
                NodeId = "node-c",
                ParentId = null,
                Order = 2,
                Label = "Chưa trigger",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 90_000m,
                ActionType = ScenarioActionType.SellAll,
                Status = ScenarioNodeStatus.Pending
            }
        });

        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var alert1 = new AlertHistory("user-1", plan.Id, "ScenarioPlaybook",
            "[VNM] Kịch bản: Trigger sớm", "Bán 20%",
            symbol: "VNM", currentValue: 82_500m, thresholdValue: 82_000m);
        typeof(AlertHistory).GetProperty("TriggeredAt")!
            .SetValue(alert1, new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc));

        var alert2 = new AlertHistory("user-1", plan.Id, "ScenarioPlaybook",
            "[VNM] Kịch bản: Trigger muộn", "Bán 30%",
            symbol: "VNM", currentValue: 85_500m, thresholdValue: 85_000m);
        typeof(AlertHistory).GetProperty("TriggeredAt")!
            .SetValue(alert2, new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc));

        _alertHistoryRepo.Setup(r => r.GetByAlertRuleIdAsync(plan.Id, "ScenarioPlaybook", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory> { alert1, alert2 });

        var query = new GetScenarioHistoryQuery { TradePlanId = plan.Id, UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — triggered nodes come first sorted by TriggeredAt desc, then pending nodes
        result.Should().HaveCount(3);
        var triggered = result.Where(r => r.Status == "Triggered").ToList();
        triggered.Should().HaveCount(2);
        triggered[0].NodeId.Should().Be("node-b"); // later trigger first
        triggered[1].NodeId.Should().Be("node-a"); // earlier trigger second
    }
}
