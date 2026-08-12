using FluentAssertions;
using InvestmentApp.Application.Decisions.DTOs;
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

    [Fact]
    public void FormatCashNetWorthSection_PartialCash_FlagsBothCashAndCapitalAsIncomplete()
    {
        // Lấy được cash của 1/2 danh mục: in con số trần sẽ khiến advisor tưởng đó là tổng thật,
        // rồi sizing chạy trên nền vốn thiếu — đúng hình thái của bug gốc.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 200_000_000m, portfolioCash: 10_000_000m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 1);

        section.Should().Contain("<portfolio_cash>10,000,000 VND (chưa đầy đủ: 1 danh mục không lấy được)</portfolio_cash>");
        section.Should().Contain("<investable_capital>200,000,000 VND (chưa đầy đủ: 1 danh mục không lấy được)</investable_capital>");
    }

    // --- Tiền bán chờ về T+2 ---

    [Fact]
    public void Tien_ban_cho_ve_duoc_in_tach_khoi_portfolio_cash()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 287_903_688m, portfolioCash: 287_903_688m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 30_000_000m,
            closuresKnownThrough: new DateTime(2026, 9, 2));

        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<portfolio_cash_pending>30,000,000 VND</portfolio_cash_pending>");
        section.Should().Contain("<market_closures_known_through>2026-09-02</market_closures_known_through>");
    }

    [Fact]
    public void Khong_co_tien_cho_ve_thi_khong_in_tag_pending()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 100_000_000m, portfolioCash: 100_000_000m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 0m,
            closuresKnownThrough: new DateTime(2026, 9, 2));

        section.Should().NotContain("portfolio_cash_pending");
    }

    [Fact]
    public void Thieu_trades_thi_pending_la_na_tuyet_doi_khong_phai_0()
    {
        // Ba trạng thái phải phân biệt: null = chưa tính được, 0 = không có gì chờ, >0 = số thật.
        // Gộp null với 0 là nói "không có tiền chờ" khi thật ra là "chưa biết".
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 0m, portfolioCash: null, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 1,
            pendingSettlementCash: null,
            closuresKnownThrough: null);

        section.Should().Contain("<portfolio_cash>n/a</portfolio_cash>");
        section.Should().Contain("<portfolio_cash_pending>n/a</portfolio_cash_pending>");
        section.Should().NotContain("<portfolio_cash_pending>0 VND</portfolio_cash_pending>");
    }

    [Fact]
    public void Chua_nhap_lich_nghi_nao_thi_known_through_la_na()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 100_000_000m, portfolioCash: 100_000_000m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 5_000_000m,
            closuresKnownThrough: null);

        section.Should().Contain("<market_closures_known_through>n/a</market_closures_known_through>");
    }

    [Fact]
    public void Khong_bao_gio_in_pending_ben_trong_mot_portfolio_cash_dang_la_na()
    {
        // Mỗi fetch trong bản tin được bọc riêng nên trades lấy được mà netFlow chết là
        // chuyện có thật. Nếu điều kiện null của pending lệch với cash, bản tin in một con số
        // chờ về nằm bên trong một tổng tiền không biết là bao nhiêu.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 0m, portfolioCash: null, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 1,
            pendingSettlementCash: null,
            closuresKnownThrough: new DateTime(2026, 9, 2));

        section.Should().Contain("<portfolio_cash>n/a</portfolio_cash>");
        section.Should().Contain("<portfolio_cash_pending>n/a</portfolio_cash_pending>");
        section.Should().NotMatchRegex(@"<portfolio_cash_pending>[\d,]+ VND</portfolio_cash_pending>");
    }

    [Fact]
    public void Call_site_cu_khong_truyen_2_tham_so_moi_van_bien_dich_va_khong_in_pending()
    {
        // Hai tham số mới có default → mọi lời gọi cũ giữ nguyên hành vi.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 100_000_000m, portfolioCash: 100_000_000m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null);

        section.Should().Contain("<portfolio_cash_pending>n/a</portfolio_cash_pending>");
    }

    [Fact]
    public void FormatCashNetWorthSection_CompleteCash_HasNoCaveat()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 200_000_000m, portfolioCash: 10_000_000m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0);

        section.Should().NotContain("chưa đầy đủ");
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

        var section = AiAssistantService.FormatPortfolioOverviewSection(
            rows, totalInvested: 205_270_000m, totalGrossBuys: 400_000_000m);

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

        var section = AiAssistantService.FormatPortfolioOverviewSection(
            rows, totalInvested: 150_000_000m, totalGrossBuys: null);

        section.Should().Contain("cash=\"n/a\"");
        section.Should().Contain("<total_cash>10,000,000 VND (chưa đầy đủ: 1 danh mục không lấy được)</total_cash>");
        // Thiếu lệnh của 1 danh mục → không đủ cơ sở tính mẫu số, thà bỏ hẳn hơn in số sai
        section.Should().NotContain("<total_return>");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_ZeroDenominators_OmitReturnsInsteadOfDividingByZero()
    {
        var rows = new List<PortfolioDigestRow> { new("A", 0m, 0m, 0m, 0m) };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 0m, totalGrossBuys: 0m);

        section.Should().NotContain("<unrealized_return>");
        section.Should().NotContain("<total_return>");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_TwoReturnMetrics_UseTheirOwnDenominators()
    {
        // Lỗi đã bắt được khi soi payload thật: cộng realized vào tử số nhưng mẫu số vẫn là
        // giá vốn phần ĐANG NẮM → -39.7% trong khi vị thế thực chỉ lỗ -19.8%.
        var rows = new List<PortfolioDigestRow>
        {
            new("24hmoney", MarketValue: 144_565_000m, Cash: 0m,
                UnrealizedPnL: -35_612_000m, RealizedPnL: -35_922_000m),
        };

        var section = AiAssistantService.FormatPortfolioOverviewSection(
            rows, totalInvested: 180_177_000m, totalGrossBuys: 360_354_000m);

        // -35,612,000 / 180,177,000 = -19.8%
        section.Should().Contain("<unrealized_return>-19.8%");
        // (-35,612,000 + -35,922,000) / 360,354,000 = -19.9%
        section.Should().Contain("<total_return>-19.9%");
        section.Should().NotContain("-39.7%");
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

    // --- FormatRecentTradesSection: lệnh gần đây (làm hiện việc "đã bán nửa HHV") ---

    [Fact]
    public void FormatRecentTradesSection_RendersSellTradeWithPortfolioAndValue()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 24), "24hmoney", "HHV", IsBuy: false,
                Quantity: 14_500m, Price: 9_950m, GrossValue: 144_275_000m),
        };

        var section = AiAssistantService.FormatRecentTradesSection(rows);

        section.Should().Contain("<recent_trades>");
        section.Should().Contain("</recent_trades>");
        section.Should().Contain("24/07/2026");
        section.Should().Contain("24hmoney");
        section.Should().Contain("HHV");
        section.Should().Contain("BÁN");
        section.Should().Contain("14,500");
        section.Should().Contain("144,275,000");
        // Nhãn phải nói rõ đây là giá trị gộp — tiền thực về đã trừ phí/thuế nên nhỏ hơn
        section.Should().Contain("chưa gồm phí/thuế");
    }

    [Fact]
    public void FormatRecentTradesSection_BuyTrade_LabelledMua()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 20), "Swing Trading", "HPG", true, 1_000m, 25_000m, 25_000_000m),
        };

        AiAssistantService.FormatRecentTradesSection(rows).Should().Contain("MUA");
    }

    [Fact]
    public void FormatRecentTradesSection_SortedNewestFirst()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 10), "A", "OLD", true, 1m, 1_000m, 1_000m),
            new(new DateTime(2026, 7, 24), "A", "NEW", true, 1m, 1_000m, 1_000m),
        };

        var section = AiAssistantService.FormatRecentTradesSection(rows);

        section.IndexOf("NEW", StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("OLD", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatRecentTradesSection_MoreThan20Rows_StatesHowManyOmitted()
    {
        var rows = Enumerable.Range(1, 23)
            .Select(i => new TradeDigestRow(new DateTime(2026, 7, 20), "A", $"S{i:00}", true, 1m, 1_000m, 1_000m))
            .ToList();

        AiAssistantService.FormatRecentTradesSection(rows).Should().Contain("còn 3 lệnh khác");
    }

    [Fact]
    public void FormatRecentTradesSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatRecentTradesSection(Array.Empty<TradeDigestRow>()).Should().BeEmpty();
    }

    // --- FormatDecisionQueueSection: "hôm nay cần quyết gì" ---

    [Fact]
    public void FormatDecisionQueueSection_RendersCriticalFirstWithPortfolioAndHeadline()
    {
        var items = new List<DecisionItemDto>
        {
            new()
            {
                Id = "ThesisReviewDue:tp2", Type = DecisionType.ThesisReviewDue,
                Severity = DecisionSeverity.Warning, Symbol = "FPT", PortfolioName = "24hmoney",
                Headline = "FPT quá hạn review thesis 4 ngày", ThesisOrReason = "Chờ KQKD Q2",
            },
            new()
            {
                Id = "StopLossHit:tp1", Type = DecisionType.StopLossHit,
                Severity = DecisionSeverity.Critical, Symbol = "HHV", PortfolioName = "24hmoney",
                Headline = "HHV xuyên SL 10.000 (giá 9.970)", ThesisOrReason = "Hạ tầng hưởng lợi đầu tư công",
                CurrentPrice = 9_970m, PlannedExitPrice = 10_000m,
            },
        };

        var section = AiAssistantService.FormatDecisionQueueSection(items);

        section.Should().Contain("<decision_queue>");
        section.Should().Contain("</decision_queue>");
        section.Should().Contain("HHV xuyên SL 10.000 (giá 9.970)");
        section.Should().Contain("24hmoney");
        section.Should().Contain("Hạ tầng hưởng lợi đầu tư công");
        section.Should().Contain("Chờ KQKD Q2");
        // Critical phải đứng trước Warning
        section.IndexOf("HHV", StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("FPT", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatDecisionQueueSection_LabelsSeverityInVietnamese()
    {
        var items = new List<DecisionItemDto>
        {
            new() { Id = "x", Type = DecisionType.ScenarioTrigger, Severity = DecisionSeverity.Critical,
                    Symbol = "MWG", PortfolioName = "A", Headline = "MWG trigger bán 30%" },
        };

        AiAssistantService.FormatDecisionQueueSection(items).Should().Contain("Gấp");
    }

    [Fact]
    public void FormatDecisionQueueSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatDecisionQueueSection(Array.Empty<DecisionItemDto>()).Should().BeEmpty();
    }

    // --- FormatRiskAlertsSection: luật theo rủi ro thay vì ngưỡng lỗ tuyệt đối ---

    [Fact]
    public void FormatRiskAlertsSection_BreachedStopLoss_FlaggedFirst()
    {
        var rows = new[]
        {
            Pos("HHV", "24hmoney", sizePct: 87.6m, sl: 10_000m, distSl: -0.3m),
            // HPG phải an toàn trên MỌI luật — kể cả luật lỗ nặng, nên ghi đè L/L mặc định của Pos().
            Pos("HPG", "Swing Trading", sizePct: 5m, sl: 20_000m, distSl: 12m) with { UnrealizedPnLPercent = 3m },
        };

        var section = AiAssistantService.FormatRiskAlertsSection(rows);

        section.Should().Contain("<risk_alerts>");
        section.Should().Contain("</risk_alerts>");
        section.Should().Contain("HHV");
        section.Should().Contain("xuyên stop-loss");
        section.Should().NotContain("HPG");
    }

    [Fact]
    public void FormatRiskAlertsSection_NearStopLossWithin3Percent_Flagged()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("MWG", "A", sizePct: 5m, sl: 50_000m, distSl: 2.1m) });

        section.Should().Contain("sát stop-loss");
    }

    [Fact]
    public void FormatRiskAlertsSection_ConcentrationAtLeast30Percent_FlaggedWithActualPercent()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("HHV", "24hmoney", sizePct: 87.6m, sl: 5_000m, distSl: 50m) });

        section.Should().Contain("tập trung quá mức");
        section.Should().Contain("87.6%");
    }

    [Fact]
    public void FormatRiskAlertsSection_MissingStopLoss_Flagged()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("FPT", "24hmoney", sizePct: 5m, sl: null, distSl: null) });

        section.Should().Contain("chưa đặt stop-loss");
    }

    [Fact]
    public void FormatRiskAlertsSection_LossAtOrBeyondMinus15Percent_Flagged()
    {
        var row = Pos("MWG", "A", sizePct: 5m, sl: 5_000m, distSl: 40m) with { UnrealizedPnLPercent = -16m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().Contain("lỗ nặng");
    }

    [Fact]
    public void FormatRiskAlertsSection_LossOnlyMinus8Percent_NotFlagged()
    {
        // Ngưỡng cũ -5% quá nhiễu; nay -15%
        var row = Pos("MWG", "A", sizePct: 5m, sl: 5_000m, distSl: 40m) with { UnrealizedPnLPercent = -8m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().BeEmpty();
    }

    [Fact]
    public void FormatRiskAlertsSection_NoAlerts_ReturnsEmpty()
    {
        var row = Pos("HPG", "A", sizePct: 5m, sl: 20_000m, distSl: 15m) with { UnrealizedPnLPercent = 3m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().BeEmpty();
    }

    // --- FormatDrillDownSection: cho agent biết còn tool nào để tra sâu hơn ---

    [Fact]
    public void FormatDrillDownSection_ListsToolsForDeeperQuestions()
    {
        var section = AiAssistantService.FormatDrillDownSection();

        section.Should().Contain("<drill_down>");
        section.Should().Contain("</drill_down>");
        section.Should().Contain("get_performance");
        section.Should().Contain("get_technical_analysis");
        section.Should().Contain("get_discipline_score");
    }
}
