using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

public class TradePlanPriceAdjusterTests
{
    private static readonly DateTime Created = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExDate = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TradePlan PlanCreatedAt(DateTime createdAt, decimal entryPrice = 30_000m)
    {
        var plan = new TradePlan("user-1", "HPG", "Buy", entryPrice, 27_000m, 36_000m, 100,
            portfolioId: "port-1",
            thesis: "Luận điểm đủ dài để qua được ràng buộc tối thiểu của entity");
        // CreatedAt/PricesSetAt là private set — đẩy lùi bằng reflection cho test thuần.
        typeof(TradePlan).GetProperty(nameof(TradePlan.CreatedAt))!
            .SetValue(plan, createdAt);
        typeof(TradePlan).GetProperty(nameof(TradePlan.PricesSetAt))!
            .SetValue(plan, createdAt);
        return plan;
    }

    private static CorporateAction StockDividend30() =>
        CorporateAction.StockDividend("port-1", "user-1", "HPG", 10, 13, ExDate, null);

    private static CorporateAction CashDividend5() =>
        CorporateAction.CashDividend("port-1", "user-1", "HPG", 5m, ExDate, null, 5m);

    [Fact]
    public void AdjustedEntryPrice_DividesByMultiplier_ForStockDividend()
    {
        var plan = PlanCreatedAt(Created);

        var adjusted = TradePlanPriceAdjuster.AdjustedEntryPrice(plan, new[] { StockDividend30() });

        // 30.000 / 1,3 — cùng hệ số mà giá thị trường bị điều chỉnh
        adjusted.Should().BeApproximately(23_076.92m, 0.01m);
    }

    [Fact]
    public void AdjustedEntryPrice_Unchanged_WhenActionPrecedesTheAnchor()
    {
        // Kế hoạch lập SAU ngày GDKHQ → giá nhập đã ở mặt bằng mới, không được hạ thêm lần nữa
        var plan = PlanCreatedAt(ExDate.AddDays(5), entryPrice: 23_000m);

        var adjusted = TradePlanPriceAdjuster.AdjustedEntryPrice(plan, new[] { StockDividend30() });

        adjusted.Should().Be(23_000m);
    }

    [Theory]
    [InlineData(ScenarioConditionType.PriceAbove)]
    [InlineData(ScenarioConditionType.PriceBelow)]
    public void AdjustedConditionValue_AdjustsPriceThresholds(ScenarioConditionType type)
    {
        var plan = PlanCreatedAt(Created);
        var node = new ScenarioNode { ConditionType = type, ConditionValue = 39_000m };

        var adjusted = TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, new[] { StockDividend30() });

        adjusted.Should().Be(30_000m); // 39.000 / 1,3
    }

    [Theory]
    [InlineData(ScenarioConditionType.PricePercentChange)]
    [InlineData(ScenarioConditionType.TimeElapsed)]
    public void AdjustedConditionValue_LeavesNonPriceThresholdsAlone(ScenarioConditionType type)
    {
        var plan = PlanCreatedAt(Created);
        var node = new ScenarioNode { ConditionType = type, ConditionValue = 10m };

        var adjusted = TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, new[] { StockDividend30() });

        adjusted.Should().Be(10m); // phần trăm / số ngày, không phải giá
    }

    [Fact]
    public void AdjustedActivationPrice_AdjustsAbsolutePrice()
    {
        var plan = PlanCreatedAt(Created);
        var config = new TrailingStopConfig { ActivationPrice = 39_000m };

        var adjusted = TradePlanPriceAdjuster.AdjustedActivationPrice(config, plan, new[] { StockDividend30() });

        adjusted.Should().Be(30_000m);
    }

    [Fact]
    public void AdjustedTrailValue_ScalesFixedAmount_ButIgnoresCashDividend()
    {
        // Biên trượt là KHOẢNG CÁCH giá: co theo hệ số chia, nhưng cổ tức tiền mặt
        // dịch cả mặt bằng giá nên không làm khoảng cách hẹp lại.
        var plan = PlanCreatedAt(Created);
        var config = new TrailingStopConfig { Method = TrailingStopMethod.FixedAmount, TrailValue = 1_300m };

        var adjusted = TradePlanPriceAdjuster.AdjustedTrailValue(
            config, plan, new[] { StockDividend30(), CashDividend5() });

        adjusted.Should().Be(1_000m); // chỉ chia 1,3; không trừ 500đ cổ tức
    }

    [Fact]
    public void AdjustedTrailValue_LeavesPercentageAlone()
    {
        var plan = PlanCreatedAt(Created);
        var config = new TrailingStopConfig { Method = TrailingStopMethod.Percentage, TrailValue = 8m };

        var adjusted = TradePlanPriceAdjuster.AdjustedTrailValue(config, plan, new[] { StockDividend30() });

        adjusted.Should().Be(8m);
    }

    [Fact]
    public void AdjustedEntryPrice_IgnoresAnAnnouncedActionBeforeItsExDate()
    {
        // Sự kiện thường được công bố trước ngày GDKHQ vài tuần. Trong khoảng đó giá thị trường
        // CHƯA điều chỉnh, nên hạ giá kế hoạch sớm là so hai mặt bằng lệch nhau.
        var plan = PlanCreatedAt(Created);
        var announced = CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, DateTime.UtcNow.Date.AddDays(14), null);

        var adjusted = TradePlanPriceAdjuster.AdjustedEntryPrice(plan, new[] { announced });

        adjusted.Should().Be(30_000m);
    }

    [Fact]
    public void AdjustedTrailValue_IgnoresAnAnnouncedActionBeforeItsExDate()
    {
        var plan = PlanCreatedAt(Created);
        var config = new TrailingStopConfig { Method = TrailingStopMethod.FixedAmount, TrailValue = 1_300m };
        var announced = CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, DateTime.UtcNow.Date.AddDays(14), null);

        TradePlanPriceAdjuster.AdjustedTrailValue(config, plan, new[] { announced }).Should().Be(1_300m);
    }

    [Fact]
    public void RebaseTrailingState_DoesNothing_BeforeTheExDate()
    {
        var plan = PlanCreatedAt(Created);
        var config = new TrailingStopConfig { HighestPrice = 39_000m, CurrentTrailingStop = 35_880m };
        var announced = CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, DateTime.UtcNow.Date.AddDays(14), null);

        var rebased = TradePlanPriceAdjuster.RebaseTrailingState(config, plan, new[] { announced });

        rebased.Should().BeFalse();
        config.HighestPrice.Should().Be(39_000m);
        config.PriceBasisAt.Should().BeNull();
    }

    [Fact]
    public void EditingScenarioNodes_DoesNotStopTheEntryPriceFromBeingAdjusted()
    {
        // Mốc dùng chung sẽ làm thao tác "sửa nhánh kịch bản" vô hiệu hoá việc điều chỉnh
        // giá nhập, dù giá nhập không hề được đặt lại.
        var plan = PlanCreatedAt(Created);
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(new List<ScenarioNode>
        {
            new()
            {
                NodeId = "n1",
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 30_000m,
                ActionType = ScenarioActionType.SellAll
            }
        });

        var action = StockDividend30();

        // Giá nhập vẫn quy đổi theo sự kiện tháng 3...
        TradePlanPriceAdjuster.AdjustedEntryPrice(plan, new[] { action })
            .Should().BeApproximately(23_076.92m, 0.01m);

        // ...còn ngưỡng vừa nhập theo mặt bằng mới thì không bị hạ thêm lần nữa.
        TradePlanPriceAdjuster.AdjustedConditionValue(plan.ScenarioNodes![0], plan, new[] { action })
            .Should().Be(30_000m);
    }

    [Fact]
    public void ScenarioAnchor_FallsBackToThePlanAnchor_WhenNodesWereNeverReset()
    {
        var plan = PlanCreatedAt(Created);

        TradePlanPriceAdjuster.ScenarioAnchor(plan).Should().Be(Created);
    }

    [Fact]
    public void PriceAnchor_FallsBackToCreatedAt_ForLegacyPlansWithoutTheField()
    {
        var plan = PlanCreatedAt(Created);
        typeof(TradePlan).GetProperty(nameof(TradePlan.PricesSetAt))!.SetValue(plan, null);

        TradePlanPriceAdjuster.PriceAnchor(plan).Should().Be(Created);
    }
}
