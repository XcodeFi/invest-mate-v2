using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Services;

public class ExpectedValueTests
{
    private static OutcomeScenario S(string label, decimal probability, decimal exit)
        => new() { Label = label, ProbabilityPercent = probability, ExitPrice = exit };

    private static List<OutcomeScenario> Example() => new()
    {
        S("Chạm TP1", 35m, 120m),
        S("Đạt kế hoạch", 15m, 134m),
        S("Cắt lỗ", 50m, 90m),
    };

    private static TradePlan NewPlan(decimal entry = 100m, decimal stop = 90m, decimal target = 134m, int quantity = 100,
        string direction = "Buy")
        => new("u1", "VNM", direction, entry, stop, target, quantity,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");

    [Fact]
    public void ev_example()
    {
        // R = +2, +3,4, −1 → EV = 0,7 + 0,51 − 0,5 = 0,71R; xác suất thắng ngầm định = 35 + 15 = 50%
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Example());

        var e = ExpectedValue.ForPlan(plan);

        e.EvR.Should().BeApproximately(0.71m, 0.0001m);
        e.ImpliedWinPercent.Should().Be(50m);
        e.EvAmount.Should().BeApproximately(710m, 0.01m);   // 0,71R × 1R (1.000)
    }

    [Theory]
    [InlineData(99.5)]
    [InlineData(100.5)]
    public void probabilities_within_half_a_point_of_100_still_compute(double total)
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", (decimal)total - 50m, 120m), S("Thua", 50m, 90m) });

        ExpectedValue.ForPlan(plan).EvR.Should().NotBeNull();
    }

    [Theory]
    [InlineData(80)]
    [InlineData(99.4)]
    [InlineData(100.6)]
    [InlineData(0)]
    public void ev_null_when_probabilities_not_100(double total)
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", (decimal)total - 50m, 120m), S("Thua", 50m, 90m) });

        var e = ExpectedValue.ForPlan(plan);

        e.EvR.Should().BeNull("tổng xác suất sai thì không tính EV, không ra con số nhìn như thật");
        e.ImpliedWinPercent.Should().BeNull();
        e.EvAmount.Should().BeNull();
    }

    [Fact]
    public void ev_null_without_scenarios()
        => ExpectedValue.ForPlan(NewPlan()).EvR.Should().BeNull();

    [Fact]
    public void ev_subtracts_fees()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Example());
        // phí khứ hồi cố định 100 mỗi kịch bản = 0,1R (1R = 1.000)
        RoundTripFee fee = (entry, exit, qty) => 100m;

        var e = ExpectedValue.ForPlan(plan, fee);

        e.EvR.Should().BeApproximately(0.61m, 0.0001m);
    }

    [Fact]
    public void fee_changes_the_implied_win_probability_near_the_draw_band()
    {
        // Kịch bản thắng gộp +0,15R: phí 0,1R kéo xuống +0,05R (trong vùng hoà) nên không còn tính là thắng
        var plan = NewPlan(entry: 100m, stop: 90m);
        plan.SetOutcomeScenarios(new[] { S("Thắng nhẹ", 50m, 101.5m), S("Thua", 50m, 90m) });

        ExpectedValue.ForPlan(plan).ImpliedWinPercent.Should().Be(50m);
        ExpectedValue.ForPlan(plan, (e, x, q) => 100m).ImpliedWinPercent.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ev_null_when_an_exit_price_is_not_positive(double exit)
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", 50m, 120m), S("Lỗi", 50m, (decimal)exit) });

        ExpectedValue.ForPlan(plan).EvR.Should().BeNull();
    }

    [Fact]
    public void ev_null_when_there_is_no_1r()
    {
        var plan = NewPlan(entry: 100m, stop: 100m);   // Entry = SL: 1R = 0
        plan.SetOutcomeScenarios(Example());

        ExpectedValue.ForPlan(plan).EvR.Should().BeNull("không chia cho 0");
    }

    [Fact]
    public void sell_plans_have_no_expectation()
    {
        var plan = NewPlan(entry: 100m, stop: 110m, direction: "Sell");
        plan.SetOutcomeScenarios(Example());

        ExpectedValue.ForPlan(plan).EvR.Should().BeNull();
    }

    [Fact]
    public void default_scenarios_three_rows_zero_probability()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, target: 130m);
        plan.SetExitTargets(new List<ExitTarget>
        {
            new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 110m, PercentOfPosition = 50m },
            new() { Level = 2, ActionType = ExitActionType.TakeProfit, Price = 120m, PercentOfPosition = 50m },
        });

        var rows = ExpectedValue.DefaultScenarios(plan);

        rows.Select(r => r.Label).Should().Equal("Đạt kế hoạch", "Cắt lỗ", "Thoát sớm / hoà");
        rows.Select(r => r.ProbabilityPercent).Should().OnlyContain(p => p == 0m, "ô xác suất không điền sẵn (chống neo)");
        rows.Select(r => r.ExitPrice).Should().Equal(115m, 90m, 100m);
    }

    [Fact]
    public void default_scenarios_use_target_when_there_are_no_exit_targets()
    {
        var rows = ExpectedValue.DefaultScenarios(NewPlan(entry: 100m, stop: 90m, target: 130m));

        rows[0].ExitPrice.Should().Be(130m);
    }

    [Fact]
    public void default_scenarios_empty_for_sell_plans()
        => ExpectedValue.DefaultScenarios(NewPlan(entry: 100m, stop: 110m, direction: "Sell")).Should().BeEmpty();
}
