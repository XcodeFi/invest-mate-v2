using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using MediatR;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Wiring test cho <c>BuildDailyBriefingContext</c> — dựng service thật với repo mock.
///
/// Tái hiện đúng sự cố 2026-07-26: user đã bán nửa vị thế HHV thu ~143,9tr nhưng bản tin
/// báo tiền mặt = 0, khiến advisor kết luận "không còn dư địa xoay xở" và gợi ý khối lượng
/// mua thấp hơn thực tế. Endpoint thật xác thực bằng ApiKey scheme nên không verify được
/// bằng JWT; test này thay thế, và mạnh hơn vì chạy lại được mãi.
/// </summary>
public class AiAssistantServiceDigestWiringTests
{
    private const string UserId = "user-1";

    // Kịch bản HHV: mua 29.000 @ 12.426, bán 14.500 @ 9.950 (fee 250k, tax 110k), còn 14.500.
    private const decimal InitialCapital = 500_000_000m;
    private const decimal GrossBuys = 29_000m * 12_426m;                        // 360.354.000
    private const decimal NetSells = 14_500m * 9_950m - 250_000m - 110_000m;    // 143.915.000
    private const decimal ExpectedCash = InitialCapital - GrossBuys + NetSells; // 283.561.000

    private const decimal RemainingQty = 14_500m;
    private const decimal AvgCost = 12_426m;
    private const decimal CurrentPrice = 9_970m;
    private const decimal MarketValue = RemainingQty * CurrentPrice;            // 144.565.000
    private const decimal RealizedPnL = -35_922_000m;

    private static readonly DateTime SellDate = DateTime.UtcNow.Date.AddDays(-2);
    private static readonly DateTime BuyDate = DateTime.UtcNow.Date.AddDays(-60);

    private static AiAssistantService BuildService(out Portfolio portfolio, bool riskAvailable = true,
        bool duplicateCasedRiskSymbol = false)
    {
        portfolio = new Portfolio(UserId, "24hmoney", InitialCapital);

        var trades = new List<Trade>
        {
            new(portfolio.Id, "HHV", TradeType.BUY, 29_000m, 12_426m, 0m, 0m, BuyDate),
            new(portfolio.Id, "HHV", TradeType.SELL, 14_500m, 9_950m, 250_000m, 110_000m, SellDate),
        };

        var pnl = new PortfolioPnLSummary
        {
            TotalInvested = RemainingQty * AvgCost,
            TotalPortfolioValue = MarketValue,
            TotalUnrealizedPnL = MarketValue - RemainingQty * AvgCost,
            TotalRealizedPnL = RealizedPnL,
            Positions = new List<PositionPnL>
            {
                new()
                {
                    Symbol = "HHV", Quantity = RemainingQty, AverageCost = AvgCost,
                    CurrentPrice = CurrentPrice, RealizedPnL = RealizedPnL,
                },
            },
        };

        var risk = new PortfolioRiskSummary
        {
            PortfolioId = portfolio.Id,
            LargestPositionPercent = 87.6m,
            PositionCount = 1,
            Positions = new List<PositionRiskItem>
            {
                new()
                {
                    Symbol = "HHV", Quantity = RemainingQty, CurrentPrice = CurrentPrice,
                    MarketValue = MarketValue, PositionSizePercent = 87.6m,
                    StopLossPrice = 10_000m, DistanceToStopLossPercent = -0.3m,
                },
            },
        };

        // Trade deserialize từ Mongo không đi qua ToUpper() của ctor, còn risk service gom nhóm
        // phân biệt hoa/thường → cùng một mã có thể ra 2 item khác vỏ chữ.
        if (duplicateCasedRiskSymbol)
            risk.Positions.Add(new PositionRiskItem
            {
                Symbol = "hhv", Quantity = 1m, CurrentPrice = CurrentPrice,
                MarketValue = 9_970m, PositionSizePercent = 0.1m,
            });

        var decisionQueue = new DecisionQueueDto
        {
            TotalCount = 1,
            Items = new List<DecisionItemDto>
            {
                new()
                {
                    Id = "StopLossHit:tp1", Type = DecisionType.StopLossHit,
                    Severity = DecisionSeverity.Critical, Symbol = "HHV",
                    PortfolioId = portfolio.Id, PortfolioName = "24hmoney",
                    Headline = "HHV xuyên SL 10.000 (giá 9.970)",
                    ThesisOrReason = "Hạ tầng hưởng lợi đầu tư công",
                    CurrentPrice = CurrentPrice, PlannedExitPrice = 10_000m,
                },
            },
        };

        // Plan Draft để kiểm tra nền vốn của position sizing.
        var plan = new TradePlan(UserId, "HPG", "Buy", 25_000m, 23_000m, 30_000m, 1_000);

        var portfolioRepo = new Mock<IPortfolioRepository>();
        portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });

        var tradeRepo = new Mock<ITradeRepository>();
        tradeRepo.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        var flowRepo = new Mock<ICapitalFlowRepository>();
        flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var pnlService = new Mock<IPnLService>();
        pnlService.Setup(s => s.CalculatePortfolioPnLAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pnl);

        var riskService = new Mock<IRiskCalculationService>();
        if (riskAvailable)
            riskService.Setup(s => s.GetPortfolioRiskSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(risk);
        else
            riskService.Setup(s => s.GetPortfolioRiskSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("risk service down"));

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetDecisionQueueQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decisionQueue);

        var planRepo = new Mock<ITradePlanRepository>();
        planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var watchlistRepo = new Mock<IWatchlistRepository>();
        watchlistRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Watchlist>());

        // Không có hồ sơ tài chính → không có idle_cash. Bản tin VẪN phải in portfolio_cash.
        var profileRepo = new Mock<IFinancialProfileRepository>();
        profileRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialProfile?)null);

        // Chưa nhập lịch nghỉ nào → T+2 chỉ bỏ T7/CN, và known_through là n/a.
        var closureRepo = new Mock<IMarketClosureRepository>();
        closureRepo.Setup(r => r.GetByUserAndRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketClosure>());
        closureRepo.Setup(r => r.GetLatestDateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        return new AiAssistantService(
            new Mock<IAiSettingsRepository>().Object,
            new Mock<IAiKeyEncryptionService>().Object,
            new Mock<IAiChatServiceFactory>().Object,
            new Mock<ITradeJournalRepository>().Object,
            tradeRepo.Object,
            portfolioRepo.Object,
            pnlService.Object,
            planRepo.Object,
            new Mock<IFundamentalDataProvider>().Object,
            new Mock<IStockInfoProvider>().Object,
            new Mock<ITechnicalIndicatorService>().Object,
            riskService.Object,
            new Mock<IRiskProfileRepository>().Object,
            watchlistRepo.Object,
            new Mock<IComprehensiveStockDataProvider>().Object,
            profileRepo.Object,
            new PositionSizingService(),     // service thật → số khối lượng gợi ý là số thật
            new Mock<IMarketDataProvider>().Object,
            flowRepo.Object,
            closureRepo.Object,
            mediator.Object);
    }

    private static async Task<string> BuildPayload(bool riskAvailable = true,
        bool duplicateCasedRiskSymbol = false)
    {
        var svc = BuildService(out _, riskAvailable, duplicateCasedRiskSymbol);
        var result = await svc.BuildDailyDigestAsync(UserId);
        result.ErrorMessage.Should().BeNull();
        return result.UserMessage!;
    }

    [Fact]
    public async Task Digest_ReportsPortfolioCash_FromSoldPositionProceeds()
    {
        // ĐÂY là bug gốc: trước sửa, tiền bán HHV vô hình và portfolio_cash không tồn tại.
        var payload = await BuildPayload();

        payload.Should().Contain($"<portfolio_cash>{ExpectedCash:N0} VND</portfolio_cash>");
        payload.Should().NotContain("<portfolio_cash>0 VND</portfolio_cash>");
    }

    [Fact]
    public async Task Digest_InvestableCapital_IncludesPortfolioCash()
    {
        var payload = await BuildPayload();

        payload.Should().Contain($"<investable_capital>{MarketValue + ExpectedCash:N0} VND</investable_capital>");
    }

    [Fact]
    public async Task Digest_RendersCashSection_EvenWithoutFinancialProfile()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("<cash_and_net_worth>");
        payload.Should().NotContain("<idle_cash>");
        payload.Should().NotContain("<health_score>");
    }

    [Fact]
    public async Task Digest_PerPortfolioRow_CarriesNameCashAndRealizedPnL()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("name=\"24hmoney\"");
        payload.Should().Contain($"cash=\"{ExpectedCash:N0}\"");
        payload.Should().Contain($"realized=\"{RealizedPnL:+#,0;-#,0}\"");
        payload.Should().Contain($"<realized_pnl>{RealizedPnL:+#,0;-#,0} VND</realized_pnl>");
    }

    [Fact]
    public async Task Digest_RecentTrades_ShowsTheSellAndExcludesTradesOutsideWindow()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("<recent_trades>");
        payload.Should().Contain($"{SellDate:dd/MM/yyyy}");
        payload.Should().Contain("BÁN");
        // Lệnh mua cách đây 60 ngày nằm ngoài cửa sổ 14 ngày → không được xuất hiện
        payload.Should().NotContain($"{BuyDate:dd/MM/yyyy}");
    }

    [Fact]
    public async Task Digest_Positions_AttributeEachPositionToItsPortfolio()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("<positions>");
        payload.Should().Contain("| HHV | 24hmoney |");
        payload.Should().Contain("87.6%");
    }

    [Fact]
    public async Task Digest_IncludesDecisionQueueAndDrillDown()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("<decision_queue>");
        payload.Should().Contain("HHV xuyên SL 10.000 (giá 9.970)");
        payload.Should().Contain("<drill_down>");
        payload.Should().Contain("get_performance");
    }

    [Fact]
    public async Task Digest_RiskAlerts_FlagBreachedStopLossAndConcentration()
    {
        var payload = await BuildPayload();

        payload.Should().Contain("xuyên stop-loss");
        payload.Should().Contain("tập trung quá mức");
    }

    [Fact]
    public async Task Digest_PositionSizing_UsesCapitalIncludingCash_NotMarketValueOnly()
    {
        var payload = await BuildPayload();

        var withCash = await SuggestedShares(MarketValue + ExpectedCash);
        var brokenBaseline = await SuggestedShares(MarketValue);

        withCash.Should().BeGreaterThan(brokenBaseline, "nền vốn đúng phải lớn hơn nền vốn cũ (thiếu tiền mặt)");
        payload.Should().Contain($"{withCash:N0} cp");
        payload.Should().NotContain($"{brokenBaseline:N0} cp");
    }

    private static async Task<decimal> SuggestedShares(decimal capital)
    {
        var plan = new TradePlan(UserId, "HPG", "Buy", 25_000m, 23_000m, 30_000m, 1_000);
        var req = AiAssistantService.BuildPlanSizingRequest(plan, capital);
        var sizing = await new PositionSizingService().CalculateAsync(req);
        return sizing.Models.First(m => m.Model == sizing.RecommendedModel).Shares;
    }

    [Fact]
    public async Task Digest_DuplicateCasedSymbolInRiskData_StillRendersInsteadOfKillingWholeDigest()
    {
        // Một dòng trade cũ ghi "hhv" thay vì "HHV" từng đủ để ném ArgumentException từ
        // ToDictionary và biến CẢ bản tin thành ErrorMessage. Mọi block khác đều degrade
        // từng phần, chỗ này không được là ngoại lệ.
        var payload = await BuildPayload(duplicateCasedRiskSymbol: true);

        payload.Should().Contain("<positions>");
        payload.Should().Contain($"<portfolio_cash>{ExpectedCash:N0} VND</portfolio_cash>");
    }

    [Fact]
    public async Task Digest_RiskServiceDown_ShowsNaAndKeepsRestOfPayload()
    {
        // Luật cứng: không in 0 cho dữ liệu chưa lấy được, và một block hỏng không làm hỏng bản tin.
        var payload = await BuildPayload(riskAvailable: false);

        payload.Should().Contain("n/a");
        payload.Should().Contain($"<portfolio_cash>{ExpectedCash:N0} VND</portfolio_cash>");
        payload.Should().Contain("<positions>");
        payload.Should().NotContain("tập trung quá mức");   // không có dữ liệu risk → không bịa cảnh báo
    }
}
