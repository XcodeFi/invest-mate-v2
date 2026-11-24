using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>AI so R:R của kế hoạch với ngưỡng bằng giá trị hiệu lực: số hệ thống tính khi đủ dữ liệu.</summary>
public class AiAssistantPlanRiskRewardTests
{
    private static TradePlan Plan(string direction, decimal entry, decimal stop, decimal target)
        => new("u1", "VNM", direction, entry, stop, target, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");

    [Fact]
    public void buy_plan_uses_the_computed_value()
        => AiAssistantService.PlanRiskReward(Plan("Buy", 100m, 90m, 120m)).Should().Be(2.0m);

    [Fact]
    public void buy_plan_with_exit_targets_uses_the_blended_exit_price()
    {
        var plan = Plan("Buy", 100m, 90m, 150m);   // Target 150 sẽ cho 5.0 nếu bỏ qua các mức chốt lời
        plan.SetExitTargets(new List<ExitTarget>
        {
            new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 110m, PercentOfPosition = 50m },
            new() { Level = 2, ActionType = ExitActionType.TakeProfit, Price = 130m, PercentOfPosition = 50m },
        });

        AiAssistantService.PlanRiskReward(plan).Should().Be(2.0m);
    }

    [Fact]
    public void sell_plan_falls_back_to_the_absolute_formula()
        => AiAssistantService.PlanRiskReward(Plan("Sell", 100m, 110m, 80m)).Should().Be(2.0m);

    [Fact]
    public void no_risk_gives_zero_not_a_division_by_zero()
        => AiAssistantService.PlanRiskReward(Plan("Sell", 100m, 100m, 80m)).Should().Be(0m);
}
