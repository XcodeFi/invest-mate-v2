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

    // ---------- R thực tế của một chiến dịch đã review ----------

    private static TradePlan ReviewedPlan(decimal pnl, decimal? storedR = null, string? storedBasis = null)
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);   // 1R = 1.000
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = pnl, RealizedR = storedR, RiskBasis = storedBasis });
        return plan;
    }

    private static void ClearSnapshot(TradePlan plan)
        => typeof(TradePlan).GetProperty(nameof(TradePlan.InitialRisk))!.SetValue(plan, null);

    [Fact]
    public void ResolveForReview_uses_initial_risk_snapshot()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);
        plan.MarkReady();

        var r = RMath.ResolveRealizedR(plan, pnl: 2_500m);

        r.R.Should().Be(2.5m);
        r.Basis.Should().Be(RMath.RiskBasisPlanned);
    }

    [Fact]
    public void ResolveForReview_scales_one_r_by_the_sold_fraction()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);   // 1R = 1.000
        plan.MarkReady();

        // Đã bán 40% → 1R của phần đã chốt = 400; lãi 600 = 1,5R (không phải 0,6R)
        RMath.ResolveRealizedR(plan, pnl: 600m, riskFraction: 0.4m).R.Should().Be(1.5m);
        RMath.ResolveRealizedR(plan, pnl: 600m, riskFraction: 0m).R.Should().BeNull("chưa bán gì thì không có 1R");
    }

    [Fact]
    public void ClosedRealizedR_is_null_only_while_shares_remain_open()
    {
        var open = NewPlan(entry: 100m, stop: 90m, quantity: 100);
        open.MarkReady(); open.MarkInProgress(); open.Execute("t1");
        open.MarkReviewed(new CampaignReviewData { PnLAmount = 500m, RealizedR = 0.5m, RiskBasis = "chot-ke-hoach", OpenQuantity = 10m });
        var closed = NewPlan(entry: 100m, stop: 90m, quantity: 100);
        closed.MarkReady(); closed.MarkInProgress(); closed.Execute("t1");
        closed.MarkReviewed(new CampaignReviewData { PnLAmount = 500m, RealizedR = 0.5m, RiskBasis = "chot-ke-hoach", OpenQuantity = 0m });

        RMath.ClosedRealizedR(open).Should().BeNull();
        RMath.ClosedRealizedR(closed).Should().Be(0.5m);
        RMath.ResolveRealizedR(open).R.Should().Be(0.5m, "hiển thị trên kế hoạch vẫn thấy R của phần đã chốt");
    }

    [Fact]
    public void oversold_review_has_null_r_never_zero_and_is_excluded()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        // Hình dạng b4ad04c1: chỉ có lệnh bán, PnL khớp được = 0, RealizedR không lưu (null).
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = 0m, RealizedR = null, OversoldQuantity = 14_500m, OpenQuantity = 0m,
            Flags = new List<string> { ReviewFlags.MissingBuyLink } });

        RMath.ResolveRealizedR(plan).R.Should().BeNull("suy lại từ PnL 0 sẽ ra 0R: một mẫu hoà giả");
        RMath.ClosedRealizedR(plan).Should().BeNull();
    }

    [Fact]
    public void ResolveForReview_uses_legacy_estimate_and_marks_basis()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);   // chưa qua MarkReady: không có ảnh chụp

        var r = RMath.ResolveRealizedR(plan, pnl: -1_000m);

        r.R.Should().Be(-1m);
        r.Basis.Should().Be(RMath.RiskBasisEstimated);
    }

    [Fact]
    public void ResolveForReview_null_when_no_risk()
    {
        var plan = NewPlan(entry: 100m, stop: 100m, quantity: 100);

        var r = RMath.ResolveRealizedR(plan, pnl: 500m);

        r.R.Should().BeNull();
        r.Basis.Should().BeNull();
    }

    [Fact]
    public void ResolveForReview_flags_partial_fill_when_some_lots_never_filled()
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
        {
            new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
            new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
        });
        plan.ExecuteLot(1, "t1", 100m);   // lot 2 không bao giờ khớp

        var r = RMath.ResolveRealizedR(plan, pnl: 2_000m);

        r.Basis.Should().Be(RMath.RiskBasisPartialFill);
        r.R.Should().Be(1.0m, "1R vẫn theo khối lượng dự tính (2.000), không tự co lại");
    }

    [Fact]
    public void ResolveForReviewedPlan_prefers_the_stored_value()
    {
        var plan = ReviewedPlan(pnl: 5_000m, storedR: 1.5m, storedBasis: RMath.RiskBasisPlanned);

        var r = RMath.ResolveRealizedR(plan);

        r.R.Should().Be(1.5m);
        r.Basis.Should().Be(RMath.RiskBasisPlanned);
    }

    [Fact]
    public void ResolveForReviewedPlan_computes_when_stored_value_is_missing_without_touching_the_plan()
    {
        var plan = ReviewedPlan(pnl: 3_000m);
        ClearSnapshot(plan);

        var r = RMath.ResolveRealizedR(plan);

        r.R.Should().Be(3m);
        r.Basis.Should().Be(RMath.RiskBasisEstimated);
        plan.ReviewData!.RealizedR.Should().BeNull("tính khi đọc không được gán vào entity (sẽ bị UpdateAsync ghi xuống)");
        plan.ReviewData.RiskBasis.Should().BeNull();
    }

    [Fact]
    public void ResolveForReviewedPlan_null_for_plans_that_are_not_reviewed()
        => RMath.ResolveRealizedR(NewPlan(entry: 100m, stop: 90m, quantity: 100)).R.Should().BeNull();

    private static TradePlan ReviewedWith(CampaignReviewData data)
    {
        var plan = NewPlan(entry: 100m, stop: 90m, quantity: 100);   // 1R = 1.000
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        plan.MarkReviewed(data);
        return plan;
    }

    [Fact]
    public void open_review_with_nothing_sold_resolves_r_null_not_zero()
    {
        // Hình dạng kế hoạch 8d9ef87d sau Tính lại: còn 100 CP, chưa bán gì → PnL 0, mẫu số 1R = 0 nên RealizedR lưu là null.
        var plan = ReviewedWith(new CampaignReviewData { PnLAmount = 0m, RealizedR = null, OpenQuantity = 100m });

        RMath.ResolveRealizedR(plan).R.Should().BeNull("suy lại từ PnL 0 với riskFraction 1 sẽ ra 0R giả");
        RMath.ResolveRealizedR(plan).Basis.Should().BeNull();
    }

    [Fact]
    public void new_format_review_with_null_r_is_not_rederived()
    {
        // Review do service mới tính (OpenQuantity có giá trị, kể cả 0): RealizedR/RiskBasis đã lưu là chuẩn, null nghĩa là "không tính được".
        var plan = ReviewedWith(new CampaignReviewData { PnLAmount = 500m, RealizedR = null, OpenQuantity = 0m });

        RMath.ResolveRealizedR(plan).R.Should().BeNull();
    }

    [Fact]
    public void legacy_review_without_open_quantity_still_derives_from_pnl()
    {
        // Review cũ (trước khi có OpenQuantity): vẫn suy R từ PnL đã lưu như trước.
        var plan = ReviewedWith(new CampaignReviewData { PnLAmount = 3_000m, RealizedR = null, OpenQuantity = null });

        RMath.ResolveRealizedR(plan).R.Should().Be(3m);
    }

    private static TradePlan NewPlan(decimal entry, decimal stop, int quantity, string direction = "Buy")
        => new("u1", "VNM", direction, entry, stop, 120m, quantity,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
}
