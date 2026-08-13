using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// SL của vị thế lùi về <c>TradePlan</c> khi không có bản ghi <c>stop_loss_targets</c>.
/// Bug gốc: collection đó chỉ được ghi từ trade-wizard và form tay ở trang rủi ro, nên vị thế
/// vào bằng đường khác bị báo "chưa đặt stop-loss" dù kế hoạch có SL. Xem ADR-0017.
/// </summary>
public class RiskCalculationServicePlanStopLossTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IStockPriceService> _stockPriceService = new();
    private readonly Mock<IStopLossTargetRepository> _slTargetRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<IStockPriceRepository> _stockPriceRepo = new();
    private readonly Mock<IPnLService> _pnlService = new();
    private readonly Mock<ICapitalFlowRepository> _capitalFlowRepo = new();
    private readonly Mock<IRiskProfileRepository> _riskProfileRepo = new();
    private readonly Mock<IFundamentalDataProvider> _fundamentalProvider = new();
    private readonly Mock<IComprehensiveStockDataProvider> _comprehensiveProvider = new();
    private readonly Mock<IMarketDataProvider> _marketDataProvider = new();
    private readonly Mock<ICorporateActionRepository> _corporateActionRepo = new();
    private readonly Mock<ITradePlanRepository> _tradePlanRepo = new();
    private readonly Mock<ILogger<RiskCalculationService>> _logger = new();
    private readonly RiskCalculationService _sut;

    private const string PortfolioId = "portfolio-1";
    private const string UserId = "user-1";
    private const string Symbol = "MWG";

    public RiskCalculationServicePlanStopLossTests()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio(UserId, "Real", 100_000_000m));
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { new(PortfolioId, Symbol, TradeType.BUY, 100, 74_700m) });
        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget>());
        _corporateActionRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());
        _tradePlanRepo.Setup(r => r.GetOpenByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradePlan>());
        _capitalFlowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary
            {
                TotalPortfolioValue = 7_410_000m,
                TotalInvested = 7_470_000m,
                Positions = new List<PositionPnL>()
            });
        _pnlService.Setup(s => s.CalculatePositionPnLAsync(
                PortfolioId, It.Is<StockSymbol>(ss => ss.Value == Symbol), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionPnL
            {
                Symbol = Symbol,
                Quantity = 100,
                CurrentPrice = 74_100m,
                AverageCost = 74_700m,
                RealizedPnL = 0
            });

        _sut = new RiskCalculationService(
            _portfolioRepo.Object, _tradeRepo.Object, _stockPriceService.Object,
            _slTargetRepo.Object, _snapshotRepo.Object, _stockPriceRepo.Object,
            _pnlService.Object, _capitalFlowRepo.Object, _riskProfileRepo.Object,
            _fundamentalProvider.Object, _comprehensiveProvider.Object,
            _marketDataProvider.Object, _corporateActionRepo.Object, _tradePlanRepo.Object,
            new MemoryCache(new MemoryCacheOptions()), _logger.Object);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static TradePlan Plan(
        decimal stopLoss = 64_700m,
        decimal entry = 74_700m,
        decimal target = 85_000m,
        string symbol = Symbol,
        TradePlanStatus status = TradePlanStatus.Executed)
    {
        // Thesis ≥ 15 ký tự: EnsureDisciplineGate chặn MarkReady kể cả với plan size nhỏ.
        var plan = new TradePlan(UserId, symbol, "Buy", entry, stopLoss, target, 100, PortfolioId,
            thesis: "Luận điểm kiểm thử cho kế hoạch dừng lỗ");
        switch (status)
        {
            case TradePlanStatus.Draft:
                break;
            case TradePlanStatus.Ready:
                plan.MarkReady();
                break;
            case TradePlanStatus.InProgress:
                plan.MarkReady();
                plan.MarkInProgress();
                break;
            case TradePlanStatus.Executed:
                plan.MarkReady();
                plan.MarkInProgress();
                plan.Execute("trade-1");
                break;
            case TradePlanStatus.Cancelled:
                plan.Cancel();
                break;
            case TradePlanStatus.Reviewed:
                plan.MarkReady();
                plan.MarkInProgress();
                plan.Execute("trade-1");
                plan.MarkReviewed(new CampaignReviewData());
                break;
        }
        return plan;
    }

    private void GivenPlans(params TradePlan[] plans) =>
        _tradePlanRepo.Setup(r => r.GetOpenByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans.Where(p =>
                p.Status is TradePlanStatus.Ready or TradePlanStatus.InProgress or TradePlanStatus.Executed)
                .ToList());

    private void GivenSlTarget(decimal stopLoss, decimal entry = 74_700m, decimal target = 90_000m) =>
        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget>
            {
                new("trade-1", PortfolioId, UserId, Symbol, entry, stopLoss, target)
            });

    private async Task<PositionRiskItem> Position()
    {
        var summary = await _sut.GetPortfolioRiskSummaryAsync(PortfolioId);
        return summary.Positions.Single(p => p.Symbol == Symbol);
    }

    // ─── Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task NoSlTarget_PlanExecuted_UsesPlanStopLoss()
    {
        GivenPlans(Plan(stopLoss: 64_700m));

        var pos = await Position();

        pos.StopLossPrice.Should().Be(64_700m);
    }

    [Fact]
    public async Task NoSlTarget_PlanExecuted_FillsRiskMetrics()
    {
        GivenPlans(Plan(stopLoss: 64_700m, entry: 74_700m, target: 85_000m));

        var pos = await Position();

        pos.RiskPerShare.Should().Be(10_000m);
        pos.RiskAmount.Should().Be(1_000_000m);
        pos.RiskRewardRatio.Should().BeApproximately(1.03m, 0.01m);
        pos.TargetPrice.Should().Be(85_000m);
    }

    [Fact]
    public async Task SlTargetPresent_WinsOverPlan()
    {
        GivenSlTarget(stopLoss: 70_000m);
        GivenPlans(Plan(stopLoss: 64_700m));

        var pos = await Position();

        pos.StopLossPrice.Should().Be(70_000m);
        pos.StopLossSource.Should().Be("Target");
    }

    [Fact]
    public async Task DraftPlanOnly_LeavesStopLossNull()
    {
        // Kế hoạch nháp không được làm im cảnh báo cho vị thế thật đang hở.
        _tradePlanRepo.Setup(r => r.GetOpenByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { Plan(status: TradePlanStatus.Draft) });

        var pos = await Position();

        pos.StopLossPrice.Should().BeNull();
        pos.StopLossSource.Should().BeNull();
    }

    [Theory]
    [InlineData(TradePlanStatus.Cancelled)]
    [InlineData(TradePlanStatus.Reviewed)]
    public async Task ClosedPlanOnly_LeavesStopLossNull(TradePlanStatus status)
    {
        GivenPlans(Plan(status: status));

        var pos = await Position();

        pos.StopLossPrice.Should().BeNull();
    }

    [Fact]
    public async Task TwoPlansSameSymbol_TakesMostRecentlyUpdated()
    {
        var older = Plan(stopLoss: 60_000m);
        var newer = Plan(stopLoss: 64_700m);
        typeof(TradePlan).GetProperty(nameof(TradePlan.UpdatedAt))!
            .SetValue(older, DateTime.UtcNow.AddDays(-3));
        typeof(TradePlan).GetProperty(nameof(TradePlan.UpdatedAt))!
            .SetValue(newer, DateTime.UtcNow);
        GivenPlans(older, newer);

        var pos = await Position();

        pos.StopLossPrice.Should().Be(64_700m);
    }

    [Fact]
    public async Task PlanStopLoss_AdjustedForCorporateActionAfterPriceAnchor()
    {
        var plan = Plan(stopLoss: 60_000m, entry: 100_000m, target: 120_000m);
        var anchor = DateTime.UtcNow.AddDays(-30);
        typeof(TradePlan).GetProperty(nameof(TradePlan.PricesSetAt))!.SetValue(plan, anchor);
        GivenPlans(plan);
        // Chia tách 2:1 sau mốc đặt giá → ngưỡng cũ phải chia đôi, không thì báo thủng SL sai.
        _corporateActionRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CorporateAction.StockSplit(PortfolioId, UserId, Symbol,
                    ratioOld: 1m, ratioNew: 2m, exDate: DateTime.UtcNow.AddDays(-10), settlementDate: null)
            });

        var pos = await Position();

        pos.StopLossPrice.Should().Be(30_000m);
    }

    [Fact]
    public async Task PlanWithoutTarget_KeepsStopLossButLeavesRewardNull()
    {
        GivenPlans(Plan(stopLoss: 64_700m, target: 0m));

        var pos = await Position();

        pos.StopLossPrice.Should().Be(64_700m);
        pos.TargetPrice.Should().BeNull();
        pos.RiskRewardRatio.Should().BeNull();
    }

    [Fact]
    public async Task NoSlTarget_PlanExecuted_ReportsPlanAsSource()
    {
        var plan = Plan(stopLoss: 64_700m);
        GivenPlans(plan);

        var pos = await Position();

        pos.StopLossSource.Should().Be("Plan");
        pos.TradePlanId.Should().Be(plan.Id);
    }

    [Fact]
    public async Task PlanWithZeroStopLoss_DoesNotShadowPlanThatHasOne()
    {
        // Kế hoạch không đặt ngưỡng thì không được che kế hoạch có đặt, dù nó mới hơn.
        var withSl = Plan(stopLoss: 64_700m);
        var withoutSl = Plan(stopLoss: 0m);
        typeof(TradePlan).GetProperty(nameof(TradePlan.UpdatedAt))!
            .SetValue(withSl, DateTime.UtcNow.AddDays(-3));
        typeof(TradePlan).GetProperty(nameof(TradePlan.UpdatedAt))!
            .SetValue(withoutSl, DateTime.UtcNow);
        GivenPlans(withSl, withoutSl);

        var pos = await Position();

        pos.StopLossPrice.Should().Be(64_700m);
    }

    [Fact]
    public async Task NoSlTargetNoPlan_LeavesEverythingNull()
    {
        var pos = await Position();

        pos.StopLossPrice.Should().BeNull();
        pos.StopLossSource.Should().BeNull();
        pos.TradePlanId.Should().BeNull();
        pos.RiskRewardRatio.Should().BeNull();
    }
}
