using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using MediatR;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Các hàm dựng ngữ cảnh AI nhận portfolioId / tradePlanId từ lớp điều khiển phải đối chiếu chủ sở hữu
/// với tài khoản đang gọi trước khi nạp dữ liệu. Không thuộc về tài khoản đó thì trả đúng thông báo
/// "không tìm thấy" như id không tồn tại.
/// </summary>
public class AiAssistantServiceOwnershipTests
{
    private const string Caller = "user-1";
    private const string Other = "user-2";
    private const string NotFoundPortfolio = "Không tìm thấy danh mục.";
    private const string NotFoundPlan = "Không tìm thấy kế hoạch giao dịch.";
    private const string NoTrades = "Chưa có giao dịch nào để phân tích.";

    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<IRiskCalculationService> _riskService = new();
    private readonly Mock<IRiskProfileRepository> _riskProfileRepo = new();
    private readonly Mock<IStockInfoProvider> _stockInfo = new();
    private readonly AiAssistantService _svc;

    public AiAssistantServiceOwnershipTests()
    {
        _svc = new AiAssistantService(
            new Mock<IAiSettingsRepository>().Object,
            new Mock<IAiKeyEncryptionService>().Object,
            new Mock<IAiChatServiceFactory>().Object,
            new Mock<ITradeJournalRepository>().Object,
            _tradeRepo.Object,
            _portfolioRepo.Object,
            new Mock<IPnLService>().Object,
            _planRepo.Object,
            new Mock<IFundamentalDataProvider>().Object,
            _stockInfo.Object,
            new Mock<ITechnicalIndicatorService>().Object,
            _riskService.Object,
            _riskProfileRepo.Object,
            new Mock<IWatchlistRepository>().Object,
            new Mock<IComprehensiveStockDataProvider>().Object,
            new Mock<IFinancialProfileRepository>().Object,
            new PositionSizingService(),
            new Mock<IMarketDataProvider>().Object,
            new Mock<ICapitalFlowRepository>().Object,
            new Mock<IMarketClosureRepository>().Object,
            Microsoft.Extensions.Options.Options.Create(
                new InvestmentApp.Application.Common.SettlementOptions()),
            new Mock<IMediator>().Object);
    }

    private void PortfolioExists(string id, string ownerUserId)
    {
        var p = new Portfolio(ownerUserId, "PF", 1_000_000m);
        typeof(Portfolio).GetProperty(nameof(Portfolio.Id))!.SetValue(p, id);
        _portfolioRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
    }

    private TradePlan PlanExists(string ownerUserId, string? portfolioId)
    {
        var plan = new TradePlan(ownerUserId, "HPG", "Buy", 25_000m, 23_000m, 30_000m, 1_000, portfolioId);
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private Task<AiContextResult> Build(string useCase, string? portfolioId = null, string? tradePlanId = null) =>
        _svc.BuildContextAsync(
            useCase: useCase, userId: Caller, portfolioId: portfolioId, tradePlanId: tradePlanId,
            symbol: null, question: null, year: null, month: null, message: null, history: null);

    // ---------- portfolio-review ----------

    [Fact]
    public async Task PortfolioReview_PortfolioNotOwnedByCaller_ReturnsNotFound()
    {
        PortfolioExists("pf-x", Other);

        var result = await Build("portfolio-review", portfolioId: "pf-x");

        result.ErrorMessage.Should().Be(NotFoundPortfolio);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-x", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PortfolioReview_OwnedPortfolio_LoadsPortfolioData()
    {
        PortfolioExists("pf-1", Caller);

        var result = await Build("portfolio-review", portfolioId: "pf-1");

        result.ErrorMessage.Should().NotBe(NotFoundPortfolio);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- monthly-summary ----------

    [Fact]
    public async Task MonthlySummary_PortfolioNotOwnedByCaller_ReturnsNotFound()
    {
        PortfolioExists("pf-x", Other);

        var result = await Build("monthly-summary", portfolioId: "pf-x");

        result.ErrorMessage.Should().Be(NotFoundPortfolio);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-x", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MonthlySummary_OwnedPortfolio_LoadsPortfolioData()
    {
        PortfolioExists("pf-1", Caller);

        var result = await Build("monthly-summary", portfolioId: "pf-1");

        result.ErrorMessage.Should().NotBe(NotFoundPortfolio);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- risk-assessment ----------

    [Fact]
    public async Task RiskAssessment_PortfolioNotOwnedByCaller_ReturnsNotFound()
    {
        PortfolioExists("pf-x", Other);

        var result = await Build("risk-assessment", portfolioId: "pf-x");

        result.ErrorMessage.Should().Be(NotFoundPortfolio);
        _riskService.Verify(s => s.GetPortfolioRiskSummaryAsync("pf-x", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RiskAssessment_OwnedPortfolio_LoadsRiskData()
    {
        PortfolioExists("pf-1", Caller);

        var result = await Build("risk-assessment", portfolioId: "pf-1");

        result.ErrorMessage.Should().NotBe(NotFoundPortfolio);
        _riskService.Verify(s => s.GetPortfolioRiskSummaryAsync("pf-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- trade-analysis ----------

    [Fact]
    public async Task TradeAnalysis_PortfolioNotOwnedByCaller_BehavesLikeUnknownPortfolio()
    {
        PortfolioExists("pf-x", Other);

        var result = await Build("trade-analysis", portfolioId: "pf-x");

        result.ErrorMessage.Should().Be(NoTrades);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-x", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TradeAnalysis_OwnedPortfolio_LoadsItsTrades()
    {
        PortfolioExists("pf-1", Caller);
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade("pf-1", "VNM", TradeType.BUY, 100, 50_000m) });

        var result = await Build("trade-analysis", portfolioId: "pf-1");

        result.ErrorMessage.Should().NotBe(NoTrades);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- trade-plan-advisor ----------

    [Fact]
    public async Task TradePlanAdvisor_PlanNotOwnedByCaller_ReturnsNotFound()
    {
        var plan = PlanExists(Other, "pf-x");
        PortfolioExists("pf-x", Other);

        var result = await Build("trade-plan-advisor", tradePlanId: plan.Id);

        result.ErrorMessage.Should().Be(NotFoundPlan);
        _stockInfo.Verify(p => p.GetStockDetailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _riskProfileRepo.Verify(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TradePlanAdvisor_OwnedPlanWithOwnedPortfolio_LoadsPortfolioData()
    {
        var plan = PlanExists(Caller, "pf-1");
        PortfolioExists("pf-1", Caller);

        var result = await Build("trade-plan-advisor", tradePlanId: plan.Id);

        result.ErrorMessage.Should().NotBe(NotFoundPlan);
        _riskProfileRepo.Verify(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()), Times.Once);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAndSymbolAsync("pf-1", "HPG", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TradePlanAdvisor_OwnedPlanLinkedToPortfolioNotOwned_SkipsPortfolioDataOnly()
    {
        var plan = PlanExists(Caller, "pf-x");
        PortfolioExists("pf-x", Other);

        var result = await Build("trade-plan-advisor", tradePlanId: plan.Id);

        result.ErrorMessage.Should().NotBe(NotFoundPlan);
        _stockInfo.Verify(p => p.GetStockDetailAsync("HPG", It.IsAny<CancellationToken>()), Times.Once);
        _riskProfileRepo.Verify(r => r.GetByPortfolioIdAsync("pf-x", It.IsAny<CancellationToken>()), Times.Never);
        _tradeRepo.Verify(r => r.GetByPortfolioIdAndSymbolAsync("pf-x", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
