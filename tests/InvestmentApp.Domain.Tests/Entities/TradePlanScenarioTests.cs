using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Events;

namespace InvestmentApp.Domain.Tests.Entities;

public class TradePlanScenarioTests
{
    #region Helpers

    private static TradePlan CreateDefaultPlan(
        string userId = "user-1",
        string symbol = "VNM",
        decimal entryPrice = 80_000m,
        decimal stopLoss = 75_000m,
        decimal target = 90_000m,
        int quantity = 100)
    {
        return new TradePlan(userId, symbol, "Buy",
            entryPrice, stopLoss, target, quantity);
    }

    private static CampaignReviewData CreateReviewData() => new()
    {
        PnLAmount = 8_000_000m, PnLPercent = 10m, HoldingDays = 30,
        PnLPerDay = 266_667m, AnnualizedReturnPercent = 121.67m,
        TargetAchievementPercent = 80m, TotalInvested = 80_000_000m,
        TotalReturned = 88_000_000m, TotalFees = 200_000m,
        ReviewedAt = DateTime.UtcNow
    };

    private static TradePlan CreateAdvancedPlan()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        return plan;
    }

    private static List<ScenarioNode> CreateValidTree()
    {
        var rootId = "root-1";
        var childId = "child-1";
        return new List<ScenarioNode>
        {
            new()
            {
                NodeId = rootId,
                ParentId = null,
                Order = 0,
                Label = "Chốt lời 30%",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            },
            new()
            {
                NodeId = childId,
                ParentId = rootId,
                Order = 0,
                Label = "SL về hòa vốn",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.MoveStopToBreakeven
            }
        };
    }

    #endregion

    // =====================================================================
    // SetExitStrategyMode
    // =====================================================================

    #region SetExitStrategyMode

    [Fact]
    public void SetExitStrategyMode_Advanced_ShouldSetMode()
    {
        var plan = CreateDefaultPlan();

        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        plan.ExitStrategyMode.Should().Be(ExitStrategyMode.Advanced);
    }

    [Fact]
    public void SetExitStrategyMode_Simple_ShouldSetMode()
    {
        var plan = CreateAdvancedPlan();

        plan.SetExitStrategyMode(ExitStrategyMode.Simple);

        plan.ExitStrategyMode.Should().Be(ExitStrategyMode.Simple);
    }

    [Fact]
    public void SetExitStrategyMode_OnExecutedPlan_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.Execute("trade-1");

        var act = () => plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetExitStrategyMode_OnReviewedPlan_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.Execute("trade-1");
        plan.MarkReviewed(CreateReviewData());

        var act = () => plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetExitStrategyMode_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var versionBefore = plan.Version;

        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        plan.Version.Should().Be(versionBefore + 1);
    }

    #endregion

    // =====================================================================
    // SetScenarioNodes
    // =====================================================================

    #region SetScenarioNodes

    [Fact]
    public void SetScenarioNodes_ValidTree_ShouldSetNodes()
    {
        var plan = CreateAdvancedPlan();
        var nodes = CreateValidTree();

        plan.SetScenarioNodes(nodes);

        plan.ScenarioNodes.Should().HaveCount(2);
        plan.ScenarioNodes![0].Label.Should().Be("Chốt lời 30%");
    }

    [Fact]
    public void SetScenarioNodes_NoRoot_ShouldThrow()
    {
        var plan = CreateAdvancedPlan();
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "child-1",
                ParentId = "nonexistent-parent",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            }
        };

        var act = () => plan.SetScenarioNodes(nodes);

        act.Should().Throw<ArgumentException>().WithMessage("*root*");
    }

    [Fact]
    public void SetScenarioNodes_InvalidParentId_ShouldThrow()
    {
        var plan = CreateAdvancedPlan();
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "invalid-parent",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 75_000m,
                ActionType = ScenarioActionType.SellAll
            }
        };

        var act = () => plan.SetScenarioNodes(nodes);

        act.Should().Throw<ArgumentException>().WithMessage("*parent*");
    }

    [Fact]
    public void SetScenarioNodes_InSimpleMode_ShouldThrow()
    {
        var plan = CreateDefaultPlan(); // Simple mode by default
        var nodes = CreateValidTree();

        var act = () => plan.SetScenarioNodes(nodes);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetScenarioNodes_ShouldIncrementVersion()
    {
        var plan = CreateAdvancedPlan();
        var versionBefore = plan.Version;

        plan.SetScenarioNodes(CreateValidTree());

        plan.Version.Should().BeGreaterThan(versionBefore);
    }

    #endregion

    // =====================================================================
    // TriggerScenarioNode
    // =====================================================================

    #region TriggerScenarioNode

    [Fact]
    public void TriggerScenarioNode_PendingRoot_ShouldTrigger()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;

        plan.TriggerScenarioNode(rootId);

        var root = plan.ScenarioNodes.First(n => n.NodeId == rootId);
        root.Status.Should().Be(ScenarioNodeStatus.Triggered);
        root.TriggeredAt.Should().NotBeNull();
    }

    [Fact]
    public void TriggerScenarioNode_ChildAfterParentTriggered_ShouldTrigger()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;
        var childId = plan.ScenarioNodes![1].NodeId;

        plan.TriggerScenarioNode(rootId);
        plan.TriggerScenarioNode(childId);

        var child = plan.ScenarioNodes.First(n => n.NodeId == childId);
        child.Status.Should().Be(ScenarioNodeStatus.Triggered);
    }

    [Fact]
    public void TriggerScenarioNode_ParentNotTriggered_ShouldThrow()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var childId = plan.ScenarioNodes![1].NodeId;

        var act = () => plan.TriggerScenarioNode(childId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Parent*");
    }

    [Fact]
    public void TriggerScenarioNode_AlreadyTriggered_ShouldThrow()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;
        plan.TriggerScenarioNode(rootId);

        var act = () => plan.TriggerScenarioNode(rootId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not pending*");
    }

    [Fact]
    public void TriggerScenarioNode_NonexistentNode_ShouldThrow()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());

        var act = () => plan.TriggerScenarioNode("nonexistent");

        act.Should().Throw<ArgumentException>().WithMessage("*not found*");
    }

    [Fact]
    public void TriggerScenarioNode_ShouldRaiseDomainEvent()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;
        plan.ClearDomainEvents();

        plan.TriggerScenarioNode(rootId);

        plan.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ScenarioNodeTriggeredEvent>();
        var evt = (ScenarioNodeTriggeredEvent)plan.DomainEvents.First();
        evt.TradePlanId.Should().Be(plan.Id);
        evt.NodeId.Should().Be(rootId);
        evt.UserId.Should().Be("user-1");
    }

    [Fact]
    public void TriggerScenarioNode_WithTradeId_ShouldAddToTradeIds()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;

        plan.TriggerScenarioNode(rootId, "trade-abc");

        plan.TradeIds.Should().Contain("trade-abc");
        var root = plan.ScenarioNodes.First(n => n.NodeId == rootId);
        root.TradeId.Should().Be("trade-abc");
    }

    [Fact]
    public void TriggerScenarioNode_WithoutTradeId_ShouldNotAddToTradeIds()
    {
        var plan = CreateAdvancedPlan();
        plan.SetScenarioNodes(CreateValidTree());
        var rootId = plan.ScenarioNodes![0].NodeId;

        plan.TriggerScenarioNode(rootId);

        plan.TradeIds.Should().BeNull();
    }

    #endregion

    // =====================================================================
    // TrailingStopConfig
    // =====================================================================

    #region TrailingStopConfig

    [Fact]
    public void ScenarioNode_WithTrailingStopConfig_ShouldStore()
    {
        var plan = CreateAdvancedPlan();
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Bật trailing stop",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 90_000m,
                ActionType = ScenarioActionType.ActivateTrailingStop,
                TrailingStopConfig = new TrailingStopConfig
                {
                    Method = TrailingStopMethod.Percentage,
                    TrailValue = 5m,
                    ActivationPrice = 90_000m,
                    StepSize = 500m
                }
            }
        };

        plan.SetScenarioNodes(nodes);

        var config = plan.ScenarioNodes![0].TrailingStopConfig;
        config.Should().NotBeNull();
        config!.Method.Should().Be(TrailingStopMethod.Percentage);
        config.TrailValue.Should().Be(5m);
        config.ActivationPrice.Should().Be(90_000m);
        config.StepSize.Should().Be(500m);
    }

    #endregion

    // =====================================================================
    // Default ExitStrategyMode
    // =====================================================================

    #region Defaults

    [Fact]
    public void NewPlan_ShouldDefaultToSimpleMode()
    {
        var plan = CreateDefaultPlan();

        plan.ExitStrategyMode.Should().Be(ExitStrategyMode.Simple);
        plan.ScenarioNodes.Should().BeNull();
    }

    #endregion
}
