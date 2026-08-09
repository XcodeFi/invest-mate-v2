using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>
/// <c>PricesSetAt</c> là mốc để điều chỉnh giá kế hoạch theo sự kiện quyền.
/// Không dùng <c>UpdatedAt</c> vì nó còn nhảy mỗi lần một nhánh kịch bản kích hoạt.
/// </summary>
public class TradePlanPriceAnchorTests
{
    private static TradePlan NewPlan() =>
        new("user-1", "HPG", "Buy", 30_000m, 27_000m, 36_000m, 100,
            portfolioId: "port-1",
            thesis: "Luận điểm đủ dài để qua được ràng buộc tối thiểu của entity");

    [Fact]
    public void NewPlan_AnchorsPricesAtCreation()
    {
        var plan = NewPlan();

        plan.PricesSetAt.Should().Be(plan.CreatedAt);
    }

    [Fact]
    public void Update_MovesAnchor_WhenAPriceLevelChanges()
    {
        var plan = NewPlan();
        var before = plan.PricesSetAt!.Value;

        plan.Update(entryPrice: 23_000m);

        plan.PricesSetAt.Should().BeAfter(before);
    }

    [Fact]
    public void Update_KeepsAnchor_WhenOnlyNonPriceFieldsChange()
    {
        var plan = NewPlan();
        var before = plan.PricesSetAt;

        plan.Update(notes: "Ghi chú thêm, không đụng tới giá");

        plan.PricesSetAt.Should().Be(before);
    }

    [Fact]
    public void Update_KeepsAnchor_WhenPricesAreResentUnchanged()
    {
        // Form sửa kế hoạch gửi lại TOÀN BỘ trường mỗi lần lưu, kể cả giá không đổi.
        // Dời mốc theo "có gửi giá" sẽ khiến sửa mỗi ghi chú cũng huỷ việc điều chỉnh
        // theo sự kiện quyền đã xảy ra — đúng cái lỗi tính năng này sinh ra để chặn.
        var plan = NewPlan();
        var before = plan.PricesSetAt;

        plan.Update(
            entryPrice: 30_000m, stopLoss: 27_000m, target: 36_000m,
            notes: "Chỉ sửa ghi chú");

        plan.PricesSetAt.Should().Be(before);
    }

    [Fact]
    public void SetScenarioNodes_KeepsScenarioAnchor_WhenThresholdsAreResentUnchanged()
    {
        var plan = NewPlan();
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(Nodes(36_000m));
        var before = plan.ScenarioPricesSetAt;

        plan.SetScenarioNodes(Nodes(36_000m));

        plan.ScenarioPricesSetAt.Should().Be(before);
    }

    [Fact]
    public void SetScenarioNodes_MovesScenarioAnchor_WhenAThresholdActuallyChanges()
    {
        var plan = NewPlan();
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(Nodes(36_000m));
        var before = plan.ScenarioPricesSetAt!.Value;

        plan.SetScenarioNodes(Nodes(38_000m));

        plan.ScenarioPricesSetAt.Should().BeAfter(before);
    }

    private static List<ScenarioNode> Nodes(decimal threshold) => new()
    {
        new()
        {
            NodeId = "n1",
            Label = "Chốt lời",
            ConditionType = ScenarioConditionType.PriceAbove,
            ConditionValue = threshold,
            ActionType = ScenarioActionType.SellAll
        }
    };

    [Fact]
    public void TriggerScenarioNode_KeepsAnchor()
    {
        // UpdatedAt nhảy khi kích hoạt nhánh — mốc giá thì không được nhảy theo,
        // nếu không các nhánh còn lại sẽ thôi được điều chỉnh sau ngày GDKHQ.
        var plan = NewPlan();
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "n1",
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 36_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 50m
            }
        });
        var anchor = plan.PricesSetAt;

        plan.TriggerScenarioNode("n1");

        plan.UpdatedAt.Should().BeOnOrAfter(anchor!.Value);
        plan.PricesSetAt.Should().Be(anchor);
    }

    [Fact]
    public void SetScenarioNodes_MovesOnlyTheScenarioAnchor()
    {
        // Sửa nhánh kịch bản không đặt lại giá nhập — dùng chung một mốc thì thao tác này
        // sẽ vô hiệu hoá việc điều chỉnh giá nhập theo sự kiện quyền đã xảy ra.
        var plan = NewPlan();
        var priceAnchor = plan.PricesSetAt;
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "n1",
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 36_000m,
                ActionType = ScenarioActionType.SellAll
            }
        });

        plan.PricesSetAt.Should().Be(priceAnchor);
        plan.ScenarioPricesSetAt.Should().BeOnOrAfter(priceAnchor!.Value);
    }

    [Fact]
    public void UpdateStopLossWithHistory_MovesAnchor()
    {
        var plan = NewPlan();
        var before = plan.PricesSetAt!.Value;

        plan.UpdateStopLossWithHistory(28_000m, "Nâng dừng lỗ");

        plan.PricesSetAt.Should().BeAfter(before);
    }
}
