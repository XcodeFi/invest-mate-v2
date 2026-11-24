using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>Cổng kỳ vọng: cảnh báo + lý do, không chặn cứng. Ảnh chụp ước lượng chụp khi chốt, khoá ở lần khớp đầu.</summary>
public class TradePlanEvGateTests
{
    private const string Reason = "Chấp nhận kỳ vọng âm vì đây là lệnh thử nghiệm quy mô nhỏ";

    private static OutcomeScenario S(string label, decimal probability, decimal exit)
        => new() { Label = label, ProbabilityPercent = probability, ExitPrice = exit };

    private static TradePlan NewPlan(string direction = "Buy", decimal entry = 100m, decimal stop = 90m, int quantity = 100)
        => new("u1", "VNM", direction, entry, stop, 130m, quantity,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");

    private static OutcomeScenario[] Positive() => new[] { S("Thắng", 60m, 120m), S("Thua", 40m, 90m) };   // EV = 1,2 − 0,4 = +0,8R
    private static OutcomeScenario[] Negative() => new[] { S("Thắng", 20m, 120m), S("Thua", 80m, 90m) };   // EV = 0,4 − 0,8 = −0,4R

    [Fact]
    public void mark_ready_requires_reason_when_ev_not_positive()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());

        var act = () => plan.MarkReadyWithEvGate(null);

        act.Should().Throw<EvGateException>().Which.Message.Should().Contain("Nhập kịch bản có tổng xác suất 100% và EV dương, hoặc ghi lý do (evGateReason, tối thiểu 10 ký tự)");
        plan.Status.Should().Be(TradePlanStatus.Draft, "bị từ chối thì không đổi trạng thái");
    }

    [Fact]
    public void mark_ready_requires_reason_when_nothing_was_estimated()
    {
        var plan = NewPlan();

        var act = () => plan.MarkReadyWithEvGate("   ");

        act.Should().Throw<EvGateException>();
        plan.Status.Should().Be(TradePlanStatus.Draft);
    }

    [Fact]
    public void mark_ready_requires_reason_when_probabilities_do_not_sum_to_100()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", 60m, 120m), S("Thua", 20m, 90m) });

        var act = () => plan.MarkReadyWithEvGate(null);

        act.Should().Throw<EvGateException>("tổng xác suất sai coi như chưa nhập");
    }

    [Fact]
    public void mark_ready_requires_reason_when_ev_is_exactly_zero()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", 50m, 110m), S("Thua", 50m, 90m) });   // +1R và −1R → EV 0

        var act = () => plan.MarkReadyWithEvGate(null);

        act.Should().Throw<EvGateException>();
    }

    [Fact]
    public void mark_ready_ok_with_reason_and_stores_it()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());

        plan.MarkReadyWithEvGate(Reason);

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.EvGateReason.Should().Be(Reason);
    }

    [Fact]
    public void a_too_short_reason_is_not_a_reason()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());

        var act = () => plan.MarkReadyWithEvGate("ok");

        act.Should().Throw<EvGateException>().Which.Message.Should().Contain("10 ký tự");
    }

    [Fact]
    public void mark_ready_ok_without_reason_when_ev_positive()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());

        plan.MarkReadyWithEvGate(null);

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.EvGateReason.Should().BeNull();
    }

    [Fact]
    public void a_reason_sent_with_positive_ev_is_not_stored()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());

        plan.MarkReadyWithEvGate(Reason);

        plan.EvGateReason.Should().BeNull("lý do chỉ lưu khi cổng thực sự bị vượt");
    }

    [Fact]
    public void ev_gate_uses_fees_so_a_thin_edge_can_turn_negative()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(new[] { S("Thắng", 60m, 110m), S("Thua", 40m, 90m) });   // gộp +0,6 − 0,4 = +0,2R
        RoundTripFee fee = (e, x, q) => 400m;                                              // −0,4R phí → EV −0,2R

        var act = () => plan.MarkReadyWithEvGate(null, fee);

        act.Should().Throw<EvGateException>();
    }

    [Fact]
    public void sell_plan_mark_ready_never_requires_reason()
    {
        var plan = NewPlan(direction: "Sell", entry: 100m, stop: 110m);

        plan.MarkReadyWithEvGate(null);

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.EvGateReason.Should().BeNull();
    }

    [Fact]
    public void plain_mark_ready_keeps_its_old_behaviour_without_any_gate()
    {
        var plan = NewPlan();   // không kịch bản, EV null

        plan.MarkReady();

        plan.Status.Should().Be(TradePlanStatus.Ready, "các đường ghi lại sau sự kiện và 85 test cũ không qua cổng");
    }

    [Fact]
    public void mark_ready_with_gate_on_an_already_ready_plan_is_a_no_op()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);

        plan.SetOutcomeScenarios(Negative());
        var act = () => plan.MarkReadyWithEvGate(null);

        act.Should().NotThrow();
    }

    // ---------- ảnh chụp ước lượng ----------

    [Fact]
    public void estimate_is_captured_when_the_plan_becomes_ready()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.Estimate.Should().BeNull("còn Draft thì chưa chụp");

        plan.MarkReadyWithEvGate(null);

        plan.Estimate.Should().NotBeNull();
        plan.Estimate!.EvR.Should().BeApproximately(0.8m, 0.0001m);
        plan.Estimate.ImpliedWinProbability.Should().Be(60m);
        plan.Estimate.Scenarios.Should().HaveCount(2);
        plan.Estimate.LockedAt.Should().BeNull();
    }

    [Fact]
    public void estimate_refreshes_on_edits_while_ready()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);

        plan.SetOutcomeScenarios(new[] { S("Thắng", 70m, 120m), S("Thua", 30m, 90m) });

        plan.Estimate!.ImpliedWinProbability.Should().Be(70m);
    }

    [Fact]
    public void estimate_locks_at_first_fill_and_ignores_later_edits()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);
        plan.MarkInProgress();
        plan.Estimate!.LockedAt.Should().NotBeNull();

        plan.SetOutcomeScenarios(new[] { S("Thắng", 90m, 120m), S("Thua", 10m, 90m) });

        plan.Estimate.ImpliedWinProbability.Should().Be(60m, "ảnh chụp đã khoá: calibration dùng ước lượng trước khi vào lệnh");
        plan.OutcomeScenarios!.First().ProbabilityPercent.Should().Be(90m, "bản hiện hành vẫn sửa được");
    }

    [Fact]
    public void estimate_locks_when_the_first_lot_fills_straight_from_draft()
    {
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
        {
            new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
            new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
        });
        plan.SetOutcomeScenarios(Positive());

        plan.ExecuteLot(1, "t1", 100m);

        plan.Estimate.Should().NotBeNull();
        plan.Estimate!.LockedAt.Should().NotBeNull();
    }

    [Fact]
    public void later_lot_fills_do_not_touch_a_locked_estimate()
    {
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
        {
            new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
            new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
        });
        plan.SetOutcomeScenarios(Positive());
        plan.ExecuteLot(1, "t1", 100m);
        var lockedAt = plan.Estimate!.LockedAt;
        plan.SetOutcomeScenarios(Negative());

        plan.ExecuteLot(2, "t2", 100m);

        plan.Estimate!.LockedAt.Should().Be(lockedAt);
        plan.Estimate.ImpliedWinProbability.Should().Be(60m);
    }

    [Fact]
    public void inprogress_plan_without_an_estimate_does_not_get_one_from_a_later_lot()
    {
        // Bài học đợt 1: lot sau không được chụp bản "chốt kế hoạch" giả.
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, new List<PlanLot>
        {
            new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
            new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
        });
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Estimate.Should().BeNull();
        plan.SetOutcomeScenarios(Positive());

        plan.ExecuteLot(1, "t1", 100m);

        plan.Estimate.Should().BeNull();
    }

    [Fact]
    public void restore_clears_estimate_and_reason_but_keeps_the_scenarios_the_user_typed()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());
        plan.MarkReadyWithEvGate(Reason);
        plan.Cancel();

        plan.Restore();

        plan.Estimate.Should().BeNull();
        plan.EvGateReason.Should().BeNull();
        plan.OutcomeScenarios.Should().HaveCount(2);
    }

    [Fact]
    public void setting_scenarios_on_a_reviewed_plan_is_rejected()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData());

        var act = () => plan.SetOutcomeScenarios(Negative());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void empty_scenarios_clear_the_current_list_but_not_a_locked_estimate()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);
        plan.MarkInProgress();

        plan.SetOutcomeScenarios(null);

        plan.OutcomeScenarios.Should().BeNull();
        plan.Estimate.Should().NotBeNull();
    }

    [Fact]
    public void scenarios_are_copied_so_later_changes_to_the_input_do_not_leak_in()
    {
        var input = Positive().ToList();
        var plan = NewPlan();
        plan.SetOutcomeScenarios(input);

        input[0].ProbabilityPercent = 1m;
        input.Clear();

        plan.OutcomeScenarios.Should().HaveCount(2);
        plan.OutcomeScenarios![0].ProbabilityPercent.Should().Be(60m);
    }

    // ---------- vào lệnh không qua cổng: không chặn, nhưng để lại dấu vết ----------

    private static List<PlanLot> TwoLots() => new()
    {
        new PlanLot { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
        new PlanLot { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 100 },
    };

    [Fact]
    public void fill_from_draft_without_estimate_sets_the_skipped_flag_and_does_not_throw()
    {
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());

        plan.ExecuteLot(1, "t1", 100m);   // lệnh đã xảy ra: không được chặn

        plan.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public void auto_chain_draft_to_in_progress_sets_the_skipped_flag()
    {
        var plan = NewPlan();

        plan.MarkReady();
        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public void fill_from_draft_with_negative_ev_and_no_reason_sets_the_flag()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());

        plan.MarkReady();
        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeTrue();
    }

    [Fact]
    public void fill_from_draft_with_positive_ev_does_not_set_the_flag()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());

        plan.MarkReady();
        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeFalse();
    }

    [Fact]
    public void going_through_the_gate_with_a_reason_does_not_set_the_flag()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Negative());
        plan.MarkReadyWithEvGate(Reason);

        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeFalse("đã vượt cổng có lý do");
    }

    [Fact]
    public void editing_into_negative_ev_after_the_gate_passed_and_then_filling_sets_the_flag()
    {
        var plan = NewPlan();
        plan.SetOutcomeScenarios(Positive());
        plan.MarkReadyWithEvGate(null);
        plan.SetOutcomeScenarios(Negative());   // sửa sau khi qua cổng

        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeTrue("kỳ vọng lúc khớp ≤ 0 mà không có lý do");
    }

    [Fact]
    public void sell_plans_are_never_flagged()
    {
        var plan = NewPlan(direction: "Sell", entry: 100m, stop: 110m);

        plan.MarkReady();
        plan.MarkInProgress();

        plan.EvGateSkipped.Should().BeFalse();
    }

    [Fact]
    public void a_later_lot_does_not_change_the_flag()
    {
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());
        plan.SetOutcomeScenarios(Positive());
        plan.ExecuteLot(1, "t1", 100m);
        plan.EvGateSkipped.Should().BeFalse();

        plan.SetOutcomeScenarios(Negative());
        plan.ExecuteLot(2, "t2", 100m);

        plan.EvGateSkipped.Should().BeFalse("cờ chỉ ghi một lần, ở lần khớp đầu");
    }

    [Fact]
    public void restore_clears_the_skipped_flag()
    {
        var plan = NewPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.EvGateSkipped.Should().BeTrue();
        plan.Cancel();

        plan.Restore();

        plan.EvGateSkipped.Should().BeFalse();
    }
}
