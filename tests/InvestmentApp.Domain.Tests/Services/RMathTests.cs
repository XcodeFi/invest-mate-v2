using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Services;

public class RMathTests
{
    [Fact]
    public void RiskReward_long_basic()
        => RMath.RiskReward(100m, 90m, 120m).Should().Be(2.0m);

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 110)]
    public void RiskReward_null_when_stop_not_below_entry(decimal entry, decimal stop)
        => RMath.RiskReward(entry, stop, 120m).Should().BeNull();

    [Fact]
    public void BlendedExitPrice_weights_by_percent()
    {
        var targets = new List<ExitTarget>
        {
            new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 110m, PercentOfPosition = 50m },
            new() { Level = 2, ActionType = ExitActionType.TakeProfit, Price = 120m, PercentOfPosition = 50m },
        };

        RMath.BlendedExitPrice(targets, 130m).Should().Be(115m);
    }

    [Fact]
    public void BlendedExitPrice_normalizes_when_percents_do_not_sum_to_100()
    {
        var targets = new List<ExitTarget>
        {
            new() { ActionType = ExitActionType.TakeProfit, Price = 110m, PercentOfPosition = 30m },
            new() { ActionType = ExitActionType.TakeProfit, Price = 120m, PercentOfPosition = 30m },
        };

        RMath.BlendedExitPrice(targets, null).Should().Be(115m);
    }

    [Fact]
    public void BlendedExitPrice_ignores_cut_loss_targets()
    {
        var targets = new List<ExitTarget>
        {
            new() { ActionType = ExitActionType.TakeProfit, Price = 120m, PercentOfPosition = 50m },
            new() { ActionType = ExitActionType.CutLoss, Price = 90m, PercentOfPosition = 50m },
        };

        RMath.BlendedExitPrice(targets, null).Should().Be(120m);
    }

    [Fact]
    public void BlendedExitPrice_falls_back_to_target_when_targets_missing_or_empty()
    {
        RMath.BlendedExitPrice(null, 130m).Should().Be(130m);
        RMath.BlendedExitPrice(new List<ExitTarget>(), 130m).Should().Be(130m);
    }

    [Fact]
    public void BlendedExitPrice_null_when_nothing_usable()
    {
        RMath.BlendedExitPrice(null, null).Should().BeNull();
        RMath.BlendedExitPrice(null, 0m).Should().BeNull();
    }

    [Fact]
    public void RealizedR_divides_net_pnl_by_risk()
        => RMath.RealizedR(2_000_000m, 1_000_000m).Should().Be(2.0m);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RealizedR_null_when_no_risk(decimal risk)
        => RMath.RealizedR(100m, risk).Should().BeNull();

    [Theory]
    [InlineData(0.1, TradeOutcome.Draw)]
    [InlineData(-0.1, TradeOutcome.Draw)]
    [InlineData(0, TradeOutcome.Draw)]
    [InlineData(0.11, TradeOutcome.Win)]
    [InlineData(-0.11, TradeOutcome.Loss)]
    [InlineData(2.5, TradeOutcome.Win)]
    public void Classify_band(double r, TradeOutcome expected)
        => RMath.Classify((decimal)r).Should().Be(expected);

    [Fact]
    public void EstimateLegacy_uses_first_stop_history_old_price()
    {
        var plan = NewPlan(entry: 100m, stop: 95m, quantity: 100);
        plan.UpdateStopLossWithHistory(98m);   // SL dời lên sau khi đã đặt: 1R gốc vẫn theo SL đầu

        var snap = RMath.EstimateLegacy(plan);

        snap!.StopLoss.Should().Be(95m);
        snap.RiskAmount.Should().Be(500m);
        snap.RiskBasis.Should().Be(RMath.RiskBasisEstimated);
    }

    [Fact]
    public void EstimateLegacy_zero_old_price_treated_missing_falls_back_to_current_stop()
    {
        var plan = NewPlan(entry: 100m, stop: 0m, quantity: 10);
        plan.UpdateStopLossWithHistory(90m);   // OldPrice = 0 (chưa đặt) → coi như thiếu

        var snap = RMath.EstimateLegacy(plan);

        snap!.StopLoss.Should().Be(90m);
        snap.RiskAmount.Should().Be(100m);
    }

    [Fact]
    public void EstimateLegacy_without_history_uses_current_stop()
    {
        var snap = RMath.EstimateLegacy(NewPlan(entry: 100m, stop: 95m, quantity: 100));

        snap!.StopLoss.Should().Be(95m);
        snap.RiskBasis.Should().Be(RMath.RiskBasisEstimated);
    }

    [Fact]
    public void EstimateLegacy_null_when_entry_not_above_stop()
        => RMath.EstimateLegacy(NewPlan(entry: 100m, stop: 100m, quantity: 100)).Should().BeNull();

    [Fact]
    public void EstimateLegacy_null_for_sell_plans()
        => RMath.EstimateLegacy(NewPlan(entry: 100m, stop: 110m, quantity: 100, direction: "Sell")).Should().BeNull();

    [Fact]
    public void EstimateLegacy_null_when_quantity_missing()
        => RMath.EstimateLegacy(NewPlan(entry: 100m, stop: 95m, quantity: 0)).Should().BeNull();

    // ---------- R:R hiệu lực (tính khi đọc) ----------

    [Fact]
    public void effective_rr_computed_when_inputs_valid()
    {
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 120m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: 5m);

        RMath.EffectiveRiskReward(plan).Should().Be(2.0m, "số tính thắng số gõ tay khi đủ dữ liệu");
    }

    [Fact]
    public void effective_rr_uses_blended_exit_targets()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);
        plan.SetExitTargets(new List<ExitTarget>
        {
            new() { Level = 1, ActionType = ExitActionType.TakeProfit, Price = 110m, PercentOfPosition = 50m },
            new() { Level = 2, ActionType = ExitActionType.TakeProfit, Price = 130m, PercentOfPosition = 50m },
        });

        RMath.EffectiveRiskReward(plan).Should().Be(2.0m);   // bình quân 120
    }

    [Fact]
    public void effective_rr_falls_back_to_stored_for_sell_or_missing_target()
    {
        var sell = new TradePlan("u1", "VNM", "Sell", 100m, 110m, 80m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: 3m);
        var noTarget = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 0m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: 3m);
        var stopAboveEntry = new TradePlan("u1", "VNM", "Buy", 100m, 105m, 120m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: 3m);

        RMath.EffectiveRiskReward(sell).Should().Be(3m);
        RMath.EffectiveRiskReward(noTarget).Should().Be(3m);
        RMath.EffectiveRiskReward(stopAboveEntry).Should().Be(3m);
    }

    [Fact]
    public void effective_rr_null_when_nothing_computable_and_nothing_stored()
        => RMath.EffectiveRiskReward(NewPlan(entry: 100m, stop: 100m, quantity: 100)).Should().BeNull();

    [Fact]
    public void stored_rr_not_overwritten()
    {
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 120m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: 5m);

        plan.MarkReady();
        plan.Update(notes: "sửa ghi chú");
        RMath.EffectiveRiskReward(plan);

        plan.RiskRewardRatio.Should().Be(5m, "R:R tính khi đọc, không ghi đè field đã lưu");
    }

    private static TradePlan NewPlan(decimal entry, decimal stop, int quantity, string direction = "Buy")
        => new("u1", "VNM", direction, entry, stop, 120m, quantity,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
}
