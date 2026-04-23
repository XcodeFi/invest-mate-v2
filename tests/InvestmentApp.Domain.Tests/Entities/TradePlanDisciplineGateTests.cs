using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>
/// Tests #3-10, #16 trong plan Vin-discipline §D3.
/// Discipline gate size-based: size = Quantity * EntryPrice; threshold = 0.05 * AccountBalance.
/// </summary>
public class TradePlanDisciplineGateTests
{
    private static TradePlan CreatePlan(
        decimal entryPrice = 80_000m,
        int quantity = 100,
        decimal? accountBalance = 100_000_000m,
        string direction = "Buy",
        string? thesis = null,
        List<InvalidationRule>? rules = null)
    {
        var plan = new TradePlan("user-1", "VNM", direction,
            entryPrice, stopLoss: direction == "Buy" ? entryPrice * 0.9m : entryPrice * 1.1m,
            target: direction == "Buy" ? entryPrice * 1.2m : entryPrice * 0.8m,
            quantity, accountBalance: accountBalance);
        if (thesis != null) plan.SetThesis(thesis);
        if (rules != null) plan.SetInvalidationCriteria(rules);
        return plan;
    }

    private static List<InvalidationRule> RulesWithDetail(string detail, int count = 1)
    {
        var list = new List<InvalidationRule>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new InvalidationRule
            {
                Trigger = InvalidationTrigger.EarningsMiss,
                Detail = detail
            });
        }
        return list;
    }

    // ---------------------------------------------------------------
    // Test #3 — size ≥ 5% + thesis < 30 → throw
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_LargePlanWithShortThesis_ShouldThrow()
    {
        // size = 100 * 80_000 = 8_000_000; account = 100_000_000 → 8% > 5% → require full
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: "Mua vì breakout", // < 30 chars
            rules: RulesWithDetail("BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng"));

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thesis*30*");
    }

    // ---------------------------------------------------------------
    // Test #4 — size ≥ 5% + 0 rule → throw
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_LargePlanWithNoInvalidationRule_ShouldThrow()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: "Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục",
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalidation*");
    }

    // ---------------------------------------------------------------
    // Test #5 — size ≥ 5% + rule detail < 20 → throw
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_LargePlanWithShortRuleDetail_ShouldThrow()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: "Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục",
            rules: RulesWithDetail("EPS Q1 thấp"));

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*20 ký tự*");
    }

    // ---------------------------------------------------------------
    // Test #6 — size ≥ 5% + thesis ≥ 30 + 1 valid rule → pass
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_LargePlanWithFullDiscipline_ShouldPass()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: "Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục",
            rules: RulesWithDetail("BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng"));

        var action = () => plan.MarkReady();

        action.Should().NotThrow();
        plan.Status.Should().Be(TradePlanStatus.Ready);
    }

    // ---------------------------------------------------------------
    // Test #7 — size < 5% + thesis ≥ 15 + 0 rule → pass
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_SmallPlanWithShortThesis_ShouldPass()
    {
        // size = 10 * 80_000 = 800_000; account = 100_000_000 → 0.8% < 5%
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 10, accountBalance: 100_000_000m,
            thesis: "Scalping quick breakout VNM", // ≥ 15 chars, no rule
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().NotThrow();
        plan.Status.Should().Be(TradePlanStatus.Ready);
    }

    // ---------------------------------------------------------------
    // Test #8 — size < 5% + thesis < 15 → throw
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_SmallPlanWithTooShortThesis_ShouldThrow()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 10, accountBalance: 100_000_000m,
            thesis: "breakout", // < 15 chars
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thesis*15*");
    }

    // ---------------------------------------------------------------
    // Test #9 — AccountBalance null → treat size < threshold, chỉ ép thesis ≥ 15
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_NoAccountBalance_ShouldTreatAsSmall()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 10_000, accountBalance: null,
            thesis: "Mua VNM breakout với volume cao", // ≥ 15 chars
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().NotThrow();
        plan.Status.Should().Be(TradePlanStatus.Ready);
    }

    [Fact]
    public void MarkReady_NoAccountBalance_StillRequiresMinThesis()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 10, accountBalance: null,
            thesis: "short", // < 15 chars
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thesis*15*");
    }

    // ---------------------------------------------------------------
    // Test #10 — Vietnamese diacritic "Mua Hoà Phát vì EPS Q1 +35% YoY" = 32 chars → pass
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_VietnameseDiacriticThesis_ShouldCountCorrectly()
    {
        // "Mua Hoà Phát vì EPS Q1 +35% YoY" = 31 chars — size ≥ 5% pass
        var thesis = "Mua Hoà Phát vì EPS Q1 +35% YoY";
        thesis.Length.Should().BeGreaterThanOrEqualTo(30);

        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: thesis,
            rules: RulesWithDetail("BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng"));

        var action = () => plan.MarkReady();

        action.Should().NotThrow();
        plan.Thesis.Should().Contain("Hoà Phát");
    }

    // Gate applies to MarkInProgress too (Draft → Ready → InProgress path)
    [Fact]
    public void MarkInProgress_LargePlanMissingThesis_ShouldThrow()
    {
        // Create plan that passed MarkReady but thesis stripped before InProgress — unusual path
        // More typical: gate also applies at MarkInProgress to guard auto-chain edge cases
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            thesis: "Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục",
            rules: RulesWithDetail("BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng"));
        plan.MarkReady();

        // Strip thesis (edge-case simulation)
        plan.SetInvalidationCriteria(new List<InvalidationRule>());

        var action = () => plan.MarkInProgress();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalidation*");
    }

    // ---------------------------------------------------------------
    // Test #16 — Sell direction — gate works (thesis/rule still required), flip is only about SL check elsewhere
    // ---------------------------------------------------------------
    [Fact]
    public void MarkReady_SellDirection_LargePlan_ShouldRequireFullDiscipline()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 100, accountBalance: 100_000_000m,
            direction: "Sell",
            thesis: "short", // too short
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReady_SellDirection_SmallPlanWithMinThesis_ShouldPass()
    {
        var plan = CreatePlan(
            entryPrice: 80_000m, quantity: 10, accountBalance: 100_000_000m,
            direction: "Sell",
            thesis: "Short VNM khi gãy MA20 kèm volume", // ≥ 15
            rules: null);

        var action = () => plan.MarkReady();

        action.Should().NotThrow();
        plan.Direction.Should().Be("Sell");
    }
}
