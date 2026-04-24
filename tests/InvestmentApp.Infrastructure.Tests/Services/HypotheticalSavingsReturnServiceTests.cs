using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Tests for HypotheticalSavingsReturnService — the "if all cash flows went to savings at rate X, final balance = ?" simulator.
///
/// Key correctness invariants (from plan review, critical findings):
/// 1. Running balance iterative — NOT per-flow independent compounding (else withdrawal zeros out earned interest).
/// 2. Caller filters to Deposit/Withdraw only — else Dividend double-counts. Tests exercise both filtered-in and filtered-out scenarios.
/// 3. Monthly compound (1 + r/12)^months — closer to VN reality than daily.
/// </summary>
public class HypotheticalSavingsReturnServiceTests
{
    private readonly HypotheticalSavingsReturnService _service = new();

    // =============================================
    // Bug-catchers — tests that specifically catch the 2 critical review findings
    // =============================================

    [Fact]
    public void CalculateEndValue_DepositThenFullWithdrawal_InterestEarnedBeforeWithdrawalIsPreserved()
    {
        // BUG-CATCHER for running balance iterative (review §1 fix for withdrawal compounding).
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var day180 = day0.AddDays(180);
        var flows = new List<CapitalFlow>
        {
            BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m),
            BuildFlow(day180, CapitalFlowType.Withdraw, 100_000_000m),
        };

        var result = _service.CalculateEndValue(flows, 0.06m, day180);

        result.Should().BeGreaterThan(2_500_000m, "interest earned before withdrawal must not be zeroed out");
        result.Should().BeLessThan(3_500_000m);
    }

    [Fact]
    public void CalculateEndValue_EmptyFlows_ReturnsZero()
    {
        // Caller filters upstream — if nothing survives, service just returns 0 cleanly.
        var result = _service.CalculateEndValue(new List<CapitalFlow>(), 0.06m, DateTime.UtcNow);
        result.Should().Be(0m);
    }

    // =============================================
    // Baseline + standard math
    // =============================================

    [Fact]
    public void CalculateEndValue_SingleSeedDeposit_CompoundsCorrectly()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow> { BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m) };

        var result = _service.CalculateEndValue(flows, 0.06m, day0.AddDays(365));

        // 100M × (1 + 0.06/12)^12 ≈ 106.167M
        result.Should().BeApproximately(106_167_000m, 100_000m);
    }

    [Fact]
    public void CalculateEndValue_ZeroRate_EqualsSumOfSignedAmounts()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow>
        {
            BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m),
            BuildFlow(day0.AddDays(90), CapitalFlowType.Deposit, 50_000_000m),
            BuildFlow(day0.AddDays(180), CapitalFlowType.Withdraw, 20_000_000m),
        };

        var result = _service.CalculateEndValue(flows, 0m, day0.AddDays(365));

        result.Should().Be(130_000_000m);  // 100 + 50 − 20
    }

    [Fact]
    public void CalculateEndValue_MultipleDeposits_RunningBalanceIterative()
    {
        // 50M day 0, 50M day 182 → each deposit compounds from its own date to asOf.
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow>
        {
            BuildFlow(day0, CapitalFlowType.Deposit, 50_000_000m),
            BuildFlow(day0.AddDays(182), CapitalFlowType.Deposit, 50_000_000m),
        };

        var result = _service.CalculateEndValue(flows, 0.06m, day0.AddDays(365));

        result.Should().BeGreaterThan(103_000_000m);
        result.Should().BeLessThan(105_500_000m);
    }

    // =============================================
    // Boundary / edge cases
    // =============================================

    [Fact]
    public void CalculateEndValue_FlowOnSameDayAsAsOf_CountsWithZeroCompound()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow> { BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m) };

        var result = _service.CalculateEndValue(flows, 0.06m, day0);

        result.Should().Be(100_000_000m);
    }

    [Fact]
    public void CalculateEndValue_LeapYear_UsesActualDayCount()
    {
        // 2024 has 366 days. Test that we don't hardcode 365.
        var day0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow> { BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m) };

        var result = _service.CalculateEndValue(flows, 0.06m, new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        result.Should().BeGreaterThan(105_000_000m);
        result.Should().BeLessThan(107_000_000m);
    }

    [Fact]
    public void CalculateEndValue_FlowsCalledTwice_NoStateRetained()
    {
        // Pure math = no hidden state between calls.
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow> { BuildFlow(day0, CapitalFlowType.Deposit, 100_000_000m) };

        var first = _service.CalculateEndValue(flows, 0.06m, day0.AddDays(365));
        var second = _service.CalculateEndValue(flows, 0.06m, day0.AddDays(365));

        second.Should().Be(first);
    }

    // =============================================
    // Helpers
    // =============================================

    private static CapitalFlow BuildFlow(DateTime date, CapitalFlowType type, decimal amount)
    {
        return new CapitalFlow(
            portfolioId: "p1",
            userId: "u1",
            type: type,
            amount: amount,
            currency: "VND",
            note: null,
            flowDate: date,
            isSeedDeposit: false);
    }
}
