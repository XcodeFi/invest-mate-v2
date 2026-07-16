using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
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

    // --- FormatMarketContextSection: VN-Index + độ rộng + khối ngoại (Slice: market context) ---

    [Fact]
    public void FormatMarketContextSection_RendersIndexBreadthAndForeignNet()
    {
        var section = AiAssistantService.FormatMarketContextSection(new MarketIndexData
        {
            IndexSymbol = "VNINDEX", Close = 1250.5m, ChangePercent = -1.2m,
            Advance = 150, Decline = 220, Ceiling = 10, Floor = 5,
            ForeignBuyValue = 320m, ForeignSellValue = 510m
        });

        section.Should().Contain("<market_context>");
        section.Should().Contain("</market_context>");
        section.Should().Contain("1,250.5");        // Close:N2 (invariant)
        section.Should().Contain("-1.2%");
        section.Should().Contain("Tăng 150");
        section.Should().Contain("Giảm 220");
        section.Should().Contain("Trần 10");
        section.Should().Contain("Sàn 5");
        section.Should().Contain("-190");            // foreign net = 320 - 510
        section.Should().Contain("tỷ");
    }

    [Fact]
    public void FormatMarketContextSection_Null_ReturnsEmpty()
    {
        AiAssistantService.FormatMarketContextSection(null).Should().BeEmpty();
    }

    // --- FormatWatchlistSection: bảng mã theo dõi + khoảng cách target + tín hiệu cơ hội ---

    [Fact]
    public void FormatWatchlistSection_RendersTableDistanceAndBuySignal()
    {
        var entries = new List<(WatchlistItem, StockDetailInfo?)>
        {
            (new WatchlistItem { Symbol = "HPG", TargetBuyPrice = 25_000m },
             new StockDetailInfo { Symbol = "HPG", Price = 26_500m, ChangePercent = 0.8m }),
            (new WatchlistItem { Symbol = "SSI", TargetBuyPrice = 28_500m },
             new StockDetailInfo { Symbol = "SSI", Price = 28_000m, ChangePercent = -0.3m }),
        };

        var section = AiAssistantService.FormatWatchlistSection(entries);

        section.Should().Contain("<watchlist>");
        section.Should().Contain("</watchlist>");
        section.Should().Contain("HPG");
        section.Should().Contain("26,500");
        section.Should().Contain("+0.8%");
        section.Should().Contain("+6.0%");          // (26500-25000)/25000
        section.Should().Contain("📉 SSI");          // price 28,000 ≤ target 28,500 → cơ hội mua
    }

    [Fact]
    public void FormatWatchlistSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatWatchlistSection(new List<(WatchlistItem, StockDetailInfo?)>())
            .Should().BeEmpty();
    }

    [Fact]
    public void FormatWatchlistSection_NullPrice_ShowsSymbolAndTargetOnly()
    {
        var entries = new List<(WatchlistItem, StockDetailInfo?)>
        {
            (new WatchlistItem { Symbol = "VNM", TargetBuyPrice = 70_000m }, null),
        };

        var section = AiAssistantService.FormatWatchlistSection(entries);

        section.Should().Contain("VNM");
        section.Should().Contain("70,000");
        section.Should().NotContain("📉");          // không có giá → không tín hiệu
    }
}
