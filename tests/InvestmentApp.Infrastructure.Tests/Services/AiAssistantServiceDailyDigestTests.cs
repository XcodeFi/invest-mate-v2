using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Slice 3 — daily-digest: bổ sung cash/net-worth + position-sizing vào bản tin
/// (dùng cho endpoint POST /api/v1/ai/daily-digest, xác thực bằng ApiKey scheme).
///
/// Test các helper thuần (quyết định + build request + format section) — logic tích hợp
/// controller/service verify ở tầng full-stack (curl với X-Api-Key).
/// </summary>
public class AiAssistantServiceDailyDigestTests
{
    // --- ShouldComputeSizing: chỉ tính size khi entry/SL/vốn hợp lệ ---

    [Fact]
    public void ShouldComputeSizing_ValidPlan_ReturnsTrue()
    {
        AiAssistantService.ShouldComputeSizing(entryPrice: 25_000m, stopLoss: 23_000m, investableCapital: 100_000_000m)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldComputeSizing_StopLossEqualsEntry_ReturnsFalse()
    {
        // riskPerShare = 0 → size vô nghĩa (fallback 100cp gây hiểu nhầm) → bỏ qua
        AiAssistantService.ShouldComputeSizing(25_000m, 25_000m, 100_000_000m)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldComputeSizing_ZeroOrNegativeStopLoss_ReturnsFalse()
    {
        AiAssistantService.ShouldComputeSizing(25_000m, 0m, 100_000_000m).Should().BeFalse();
        AiAssistantService.ShouldComputeSizing(25_000m, -1m, 100_000_000m).Should().BeFalse();
    }

    [Fact]
    public void ShouldComputeSizing_NonPositiveCapital_ReturnsFalse()
    {
        // Không có vốn đầu tư → maxRisk = 0 → shares fallback 100cp gây hiểu nhầm → bỏ qua
        AiAssistantService.ShouldComputeSizing(25_000m, 23_000m, 0m).Should().BeFalse();
    }

    // --- BuildPlanSizingRequest: map đúng entry/SL/vốn/risk% ---

    [Fact]
    public void BuildPlanSizingRequest_MapsEntryStopAndBalance()
    {
        var plan = new TradePlan("u1", "HPG", "Buy", entryPrice: 25_000m, stopLoss: 23_000m, target: 30_000m, quantity: 1000);

        var req = AiAssistantService.BuildPlanSizingRequest(plan, investableCapital: 100_000_000m);

        req.EntryPrice.Should().Be(25_000m);
        req.StopLoss.Should().Be(23_000m);
        req.AccountBalance.Should().Be(100_000_000m);
        req.RiskPercent.Should().Be(2m, "mặc định 2% khi plan không set riskPercent");
    }

    [Fact]
    public void BuildPlanSizingRequest_UsesPlanRiskPercentWhenSet()
    {
        var plan = new TradePlan("u1", "HPG", "Buy", 25_000m, 23_000m, 30_000m, 1000, riskPercent: 1m);

        var req = AiAssistantService.BuildPlanSizingRequest(plan, 100_000_000m);

        req.RiskPercent.Should().Be(1m);
    }

    [Fact]
    public async Task BuildPlanSizingRequest_FedToPositionSizingService_RecommendsFixedRiskWith1000Shares()
    {
        // Vốn 100tr, entry 25.000, SL 23.000 → riskPerShare = 2.000, maxRisk = 2% × 100tr = 2tr
        // → shares = 2tr / 2.000 = 1.000cp. Không có ATR → recommend fixed_risk.
        var plan = new TradePlan("u1", "HPG", "Buy", 25_000m, 23_000m, 30_000m, 1000);
        var req = AiAssistantService.BuildPlanSizingRequest(plan, 100_000_000m);

        var result = await new PositionSizingService().CalculateAsync(req);

        result.RecommendedModel.Should().Be("fixed_risk");
        var rec = result.Models.First(m => m.Model == "fixed_risk");
        rec.Shares.Should().Be(1000);
    }

    // --- FormatCashNetWorthSection: render đủ tag + số liệu ---

    [Fact]
    public void FormatCashNetWorthSection_ContainsTagsAndValues()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 150_000_000m, idleCash: 50_000_000m, netWorth: 300_000_000m,
            totalAssets: 320_000_000m, totalDebt: 20_000_000m, healthScore: 78);

        section.Should().Contain("<cash_and_net_worth>");
        section.Should().Contain("</cash_and_net_worth>");
        section.Should().Contain("investable_capital");
        section.Should().Contain("net_worth");
        section.Should().Contain("78");
    }
}
