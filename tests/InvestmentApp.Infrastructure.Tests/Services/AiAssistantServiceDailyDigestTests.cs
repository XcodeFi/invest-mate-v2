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

    // --- FormatCashNetWorthSection: tách portfolio_cash (TK chứng khoán) vs idle_cash (hồ sơ tài chính) ---

    [Fact]
    public void FormatCashNetWorthSection_WithProfile_RendersBothCashSources()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 500_000_000m, portfolioCash: 287_903_688m, idleCash: 50_000_000m,
            netWorth: 300_000_000m, totalAssets: 320_000_000m, totalDebt: 20_000_000m, healthScore: 78);

        section.Should().Contain("<cash_and_net_worth>");
        section.Should().Contain("</cash_and_net_worth>");
        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<idle_cash>50,000,000 VND</idle_cash>");
        section.Should().Contain("<investable_capital>500,000,000 VND</investable_capital>");
        section.Should().Contain("78");
    }

    [Fact]
    public void FormatCashNetWorthSection_NoProfile_StillRendersPortfolioCashAndCapital()
    {
        // Bug gốc: cả block bị bọc trong `if (profile != null)` → user chưa có hồ sơ tài chính
        // thì mất sạch thông tin tiền. Nay portfolio_cash phải luôn hiện.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 452_903_688m, portfolioCash: 287_903_688m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null);

        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<investable_capital>452,903,688 VND</investable_capital>");
        // Assert theo dạng tag: chuỗi bọc ngoài <cash_and_net_worth> vốn đã chứa "net_worth".
        section.Should().NotContain("<idle_cash>");
        section.Should().NotContain("<net_worth>");
        section.Should().NotContain("<health_score>");
    }

    [Fact]
    public void FormatCashNetWorthSection_CashUnavailable_RendersNaNotZero()
    {
        // Luật cứng: không in 0 cho giá trị chưa fetch được.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 164_960_000m, portfolioCash: null, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null);

        section.Should().Contain("<portfolio_cash>n/a</portfolio_cash>");
        section.Should().NotContain("<portfolio_cash>0 VND</portfolio_cash>");
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

    // --- FormatPortfolioOverviewSection: tổng quan + bóc theo danh mục + realized P&L ---

    [Fact]
    public void FormatPortfolioOverviewSection_RendersTotalsAndPerPortfolioRows()
    {
        var rows = new List<PortfolioDigestRow>
        {
            new("24hmoney", MarketValue: 144_565_000m, Cash: 283_023_788m,
                UnrealizedPnL: -35_600_000m, RealizedPnL: -35_922_000m),
            new("Swing Trading", MarketValue: 20_395_000m, Cash: 4_879_900m,
                UnrealizedPnL: -4_700_000m, RealizedPnL: 0m),
        };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 205_270_000m);

        section.Should().Contain("<portfolio_overview>");
        section.Should().Contain("</portfolio_overview>");
        section.Should().Contain("<portfolios>2</portfolios>");
        section.Should().Contain("<total_market_value>164,960,000 VND</total_market_value>");
        section.Should().Contain("<total_cash>287,903,688 VND</total_cash>");
        section.Should().Contain("<total_capital>452,863,688 VND</total_capital>");
        section.Should().Contain("<realized_pnl>-35,922,000 VND</realized_pnl>");
        section.Should().Contain("name=\"24hmoney\"");
        section.Should().Contain("cash=\"283,023,788\"");
        section.Should().Contain("realized=\"-35,922,000\"");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_CashUnavailableForOnePortfolio_ShowsNaAndExcludesFromTotal()
    {
        // Trades repo của 1 danh mục lỗi → cash danh mục đó n/a; total_cash chỉ cộng phần lấy được
        // và phải nói rõ là chưa đầy đủ.
        var rows = new List<PortfolioDigestRow>
        {
            new("A", 100_000_000m, Cash: 10_000_000m, UnrealizedPnL: 0m, RealizedPnL: 0m),
            new("B", 50_000_000m, Cash: null, UnrealizedPnL: 0m, RealizedPnL: 0m),
        };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 150_000_000m);

        section.Should().Contain("cash=\"n/a\"");
        section.Should().Contain("<total_cash>10,000,000 VND (chưa đầy đủ: 1 danh mục không lấy được)</total_cash>");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_ZeroInvested_OmitsReturnInsteadOfDividingByZero()
    {
        var rows = new List<PortfolioDigestRow> { new("A", 0m, 0m, 0m, 0m) };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 0m);

        section.Should().NotContain("<return>");
    }

    // --- FormatPositionsSection: bảng vị thế có danh mục + %DM + khoảng cách SL ---

    private static PositionDigestRow Pos(string symbol, string portfolio, decimal? sizePct = 12m,
        decimal? sl = 11_000m, decimal? distSl = 8.5m, bool riskOk = true)
        => new(symbol, portfolio, Quantity: 14_500m, AverageCost: 12_426m, CurrentPrice: 9_970m,
               MarketValue: 144_565_000m, UnrealizedPnL: -35_600_000m, UnrealizedPnLPercent: -19.7m,
               PositionSizePercent: sizePct, StopLossPrice: sl, DistanceToStopLossPercent: distSl,
               RiskDataAvailable: riskOk);

    [Fact]
    public void FormatPositionsSection_RendersPortfolioNameQuantityCostAndRiskColumns()
    {
        var section = AiAssistantService.FormatPositionsSection(new[] { Pos("HHV", "24hmoney", 87.6m) });

        section.Should().Contain("<positions>");
        section.Should().Contain("</positions>");
        section.Should().Contain("| Mã | Danh mục | KL | Giá vốn | Giá | Giá trị | %DM | L/L % | L/L VND | SL | Cách SL |");
        section.Should().Contain("HHV");
        section.Should().Contain("24hmoney");
        section.Should().Contain("14,500");
        section.Should().Contain("12,426");
        section.Should().Contain("87.6%");
        section.Should().Contain("-19.7%");
    }

    [Fact]
    public void FormatPositionsSection_NoStopLoss_ShowsExplicitNotSetNotBlank()
    {
        // StopLossPrice null = user CHƯA ĐẶT SL → phải nói rõ, đây là tín hiệu rủi ro
        var section = AiAssistantService.FormatPositionsSection(
            new[] { Pos("MWG", "Swing Trading", sl: null, distSl: null) });

        section.Should().Contain("chưa đặt");
    }

    [Fact]
    public void FormatPositionsSection_RiskDataUnavailable_ShowsNaNotZero()
    {
        var section = AiAssistantService.FormatPositionsSection(
            new[] { Pos("FPT", "24hmoney", sizePct: null, sl: null, distSl: null, riskOk: false) });

        section.Should().Contain("n/a");
        section.Should().NotContain("0.0%");
    }

    [Fact]
    public void FormatPositionsSection_MoreThan15Rows_StatesHowManyOmitted()
    {
        var rows = Enumerable.Range(1, 18).Select(i => Pos($"S{i:00}", "24hmoney")).ToList();

        var section = AiAssistantService.FormatPositionsSection(rows);

        section.Should().Contain("còn 3 vị thế khác không hiển thị");
    }

    [Fact]
    public void FormatPositionsSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatPositionsSection(Array.Empty<PositionDigestRow>()).Should().BeEmpty();
    }
}
