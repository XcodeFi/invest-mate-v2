using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>1R cố định: chụp khi plan rời Draft, chụp lại trong Ready, khoá ở lần khớp đầu, xoá khi Restore.</summary>
public class TradePlanInitialRiskTests
{
    private static TradePlan NewPlan(decimal entry = 100m, decimal stop = 90m, int quantity = 100, string direction = "Buy")
        => new("u1", "VNM", direction, entry, stop, 130m, quantity,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");

    private static List<PlanLot> TwoLots() => new()
    {
        new PlanLot { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 100 },
        new PlanLot { LotNumber = 2, PlannedPrice = 94m, PlannedQuantity = 200 },
    };

    [Fact]
    public void draft_plan_has_no_snapshot()
        => NewPlan().InitialRisk.Should().BeNull();

    [Fact]
    public void first_exit_from_draft_captures_snapshot_via_ready()
    {
        var plan = NewPlan();

        plan.MarkReady();

        plan.InitialRisk.Should().NotBeNull();
        plan.InitialRisk!.Entry.Should().Be(100m);
        plan.InitialRisk.StopLoss.Should().Be(90m);
        plan.InitialRisk.Quantity.Should().Be(100m);
        plan.InitialRisk.RiskAmount.Should().Be(1_000m);
        plan.InitialRisk.RiskBasis.Should().Be(RMath.RiskBasisPlanned);
        plan.InitialRisk.LockedAt.Should().BeNull();
    }

    [Fact]
    public void first_exit_from_draft_captures_snapshot_via_execute_lot()
    {
        var plan = NewPlan(quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());

        plan.ExecuteLot(1, "t1", 100m);   // Draft → InProgress, không qua Ready

        plan.InitialRisk.Should().NotBeNull();
        plan.InitialRisk!.LockedAt.Should().NotBeNull("lần khớp đầu khoá ảnh chụp");
    }

    [Fact]
    public void lot_entry_is_weighted_average_of_planned_prices()
    {
        var plan = NewPlan(entry: 100m, stop: 90m);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());   // (100×100 + 94×200) / 300 = 96

        plan.MarkReady();

        plan.InitialRisk!.Entry.Should().Be(96m);
        plan.InitialRisk.Quantity.Should().Be(300m);
        plan.InitialRisk.RiskAmount.Should().Be(1_800m);
    }

    [Fact]
    public void snapshot_refreshes_on_update_while_ready()
    {
        var plan = NewPlan();
        plan.MarkReady();

        plan.Update(entryPrice: 102m, stopLoss: 92m, quantity: 200);

        plan.InitialRisk!.Entry.Should().Be(102m);
        plan.InitialRisk.StopLoss.Should().Be(92m);
        plan.InitialRisk.RiskAmount.Should().Be(2_000m);
    }

    [Fact]
    public void update_in_draft_does_not_create_snapshot()
    {
        var plan = NewPlan();

        plan.Update(entryPrice: 102m);

        plan.InitialRisk.Should().BeNull();
    }

    [Fact]
    public void snapshot_locks_at_first_fill()
    {
        var plan = NewPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        plan.InitialRisk!.LockedAt.Should().NotBeNull();

        plan.Update(entryPrice: 105m, stopLoss: 80m, quantity: 500);

        plan.InitialRisk.Entry.Should().Be(100m, "sau khi khớp, sửa giá không đổi 1R");
        plan.InitialRisk.StopLoss.Should().Be(90m);
        plan.InitialRisk.RiskAmount.Should().Be(1_000m);
    }

    [Fact]
    public void moving_stop_after_ready_keeps_initial_risk()
    {
        var plan = NewPlan();
        plan.MarkReady();

        plan.UpdateStopLossWithHistory(95m);

        plan.InitialRisk!.StopLoss.Should().Be(90m);
        plan.InitialRisk.RiskAmount.Should().Be(1_000m);
    }

    [Fact]
    public void moving_stop_after_fill_keeps_initial_risk()
    {
        var plan = NewPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        plan.UpdateStopLossWithHistory(98m);

        plan.InitialRisk!.RiskAmount.Should().Be(1_000m);
    }

    [Fact]
    public void later_lot_fills_and_stop_moves_do_not_change_a_locked_snapshot()
    {
        var plan = NewPlan(quantity: 1, stop: 90m);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());
        plan.ExecuteLot(1, "t1", 100m);
        var lockedAt = plan.InitialRisk!.LockedAt;
        var risk = plan.InitialRisk.RiskAmount;

        plan.UpdateStopLossWithHistory(97m);
        plan.ExecuteLot(2, "t2", 94m);   // khớp lot sau khi SL đã dời

        plan.InitialRisk!.RiskAmount.Should().Be(risk);
        plan.InitialRisk.StopLoss.Should().Be(90m);
        plan.InitialRisk.LockedAt.Should().Be(lockedAt);
    }

    [Fact]
    public void inprogress_with_null_snapshot_and_moved_stop_executing_next_lot_keeps_null()
    {
        // Kế hoạch cũ/ảnh chụp đầu không hợp lệ (SL = Entry) nên InitialRisk = null khi đã InProgress.
        var plan = NewPlan(entry: 100m, stop: 100m, quantity: 1);
        plan.SetLots(EntryMode.ScalingIn, TwoLots());
        plan.MarkReady();
        plan.MarkInProgress();
        plan.InitialRisk.Should().BeNull();

        plan.UpdateStopLossWithHistory(95m, "đặt SL hợp lệ");   // SL dời về mức hợp lệ
        plan.ExecuteLot(1, "t1", 100m);

        plan.InitialRisk.Should().BeNull(
            "lot khớp sau không được chụp SL đã dời rồi gắn cờ chốt-kế-hoạch: 1R sai mà trông như thật");
    }

    [Fact]
    public void restore_clears_snapshot()
    {
        var plan = NewPlan();
        plan.MarkReady();
        plan.Cancel();

        plan.Restore();

        plan.InitialRisk.Should().BeNull();
    }

    [Fact]
    public void restored_plan_captures_a_fresh_snapshot_when_it_leaves_draft_again()
    {
        var plan = NewPlan();
        plan.MarkReady();
        plan.Cancel();
        plan.Restore();
        plan.Update(entryPrice: 110m, stopLoss: 100m);

        plan.MarkReady();

        plan.InitialRisk!.Entry.Should().Be(110m);
        plan.InitialRisk.RiskAmount.Should().Be(1_000m);
    }

    [Fact]
    public void sell_plan_has_no_snapshot()
    {
        var plan = NewPlan(entry: 100m, stop: 110m, direction: "Sell");

        plan.MarkReady();

        plan.InitialRisk.Should().BeNull("chỉ lệnh mua có R ở đợt này");
    }

    [Fact]
    public void stop_not_below_entry_gives_no_snapshot()
    {
        var plan = NewPlan(entry: 100m, stop: 100m);

        plan.MarkReady();

        plan.InitialRisk.Should().BeNull("Entry = SL thì R null, không chia cho 0");
    }

    [Fact]
    public void ready_plan_edited_into_an_invalid_risk_drops_the_stale_snapshot()
    {
        var plan = NewPlan();
        plan.MarkReady();

        plan.Update(stopLoss: 100m);   // SL = Entry

        plan.InitialRisk.Should().BeNull();
    }
}
