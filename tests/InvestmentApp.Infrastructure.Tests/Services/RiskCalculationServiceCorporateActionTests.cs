using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Hai đường còn lại của <see cref="RiskCalculationService"/> từng tự dựng vị thế từ
/// <c>Trade</c> thô: kiểm tra hạn mức rủi ro (có thể KHOÁ giao dịch) và kiểm thử sức chịu đựng.
/// Cả hai phải đi qua <c>PositionBuilder</c> để không lệch sau ngày GDKHQ.
/// </summary>
public class RiskCalculationServiceCorporateActionTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IStockPriceService> _stockPriceService = new();
    private readonly Mock<IStopLossTargetRepository> _stopLossRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<IStockPriceRepository> _stockPriceRepo = new();
    private readonly Mock<IPnLService> _pnlService = new();
    private readonly Mock<ICapitalFlowRepository> _capitalFlowRepo = new();
    private readonly Mock<IRiskProfileRepository> _riskProfileRepo = new();
    private readonly Mock<IFundamentalDataProvider> _fundamentalDataProvider = new();
    private readonly Mock<IComprehensiveStockDataProvider> _comprehensiveProvider = new();
    private readonly Mock<IMarketDataProvider> _marketDataProvider = new();
    private readonly Mock<ICorporateActionRepository> _corporateActionRepo = new();
    private readonly Mock<ILogger<RiskCalculationService>> _logger = new();
    private readonly RiskCalculationService _sut;

    private static readonly DateTime Today = DateTime.UtcNow.Date;

    public RiskCalculationServiceCorporateActionTests()
    {
        _corporateActionRepo
            .Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());

        _sut = new RiskCalculationService(
            _portfolioRepo.Object, _tradeRepo.Object, _stockPriceService.Object,
            _stopLossRepo.Object, _snapshotRepo.Object, _stockPriceRepo.Object,
            _pnlService.Object, _capitalFlowRepo.Object, _riskProfileRepo.Object,
            _fundamentalDataProvider.Object, _comprehensiveProvider.Object,
            _marketDataProvider.Object, _corporateActionRepo.Object, _logger.Object);
    }

    private void SetupActions(params CorporateAction[] actions) =>
        _corporateActionRepo
            .Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);

    private void SetupBudget(IEnumerable<Trade> allTrades, IEnumerable<Trade> tradesToday,
        decimal portfolioValue = 10_000_000m, decimal lossLimitPercent = 2.0m)
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(allTrades);
        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync(
                "port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tradesToday);
        _pnlService.Setup(p => p.CalculatePortfolioPnLAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { TotalPortfolioValue = portfolioValue });
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskProfile("port-1", "user-1", dailyLossLimitPercent: lossLimitPercent));
    }

    // ─── CheckRiskBudgetAsync ────────────────────────────────────────────

    [Fact]
    public async Task CheckRiskBudget_UsesWeightedAverageCost_NotSimpleAverage()
    {
        // 100 CP @ 20.000 rồi 900 CP @ 30.000 → giá vốn bình quân GIA QUYỀN = 29.000.
        // Trung bình không trọng số là 25.000 → bán ở 28.000 sẽ hiện ra LÃI thay vì LỖ.
        var sell = new Trade("port-1", "VNM", TradeType.SELL, 1_000, 28_000m, 0, 0, Today.AddHours(10));
        SetupBudget(
            allTrades: new[]
            {
                new Trade("port-1", "VNM", TradeType.BUY, 100, 20_000m, 0, 0, Today.AddDays(-5)),
                new Trade("port-1", "VNM", TradeType.BUY, 900, 30_000m, 0, 0, Today.AddDays(-4)),
                sell
            },
            tradesToday: new[] { sell });

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.DailyPnl.Should().Be(-1_000_000m);
        result.IsLocked.Should().BeTrue(); // lỗ 10% > hạn mức 2%
    }

    [Fact]
    public async Task CheckRiskBudget_DoesNotLock_WhenLossIsOnlyTheCorporateActionAdjustment()
    {
        // Mua 100 CP @ 30.000; cổ tức cổ phiếu 10:13 → 130 CP, giá vốn còn 23.076,92.
        // Bán 130 CP @ 23.000 gần như hoà vốn. Nếu vẫn so với 30.000 thì thành lỗ giả 910k
        // và khoá giao dịch oan.
        var sell = new Trade("port-1", "HPG", TradeType.SELL, 130, 23_000m, 0, 0, Today.AddHours(10));
        SetupBudget(
            allTrades: new[]
            {
                new Trade("port-1", "HPG", TradeType.BUY, 100, 30_000m, 0, 0, Today.AddDays(-60)),
                sell
            },
            tradesToday: new[] { sell });
        SetupActions(CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, Today.AddDays(-30), Today.AddDays(-10)));

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.DailyPnl.Should().BeApproximately(-10_000m, 1m);
        result.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task CheckRiskBudget_CountsOnlyTodaysRealizedPnL()
    {
        // Lệnh bán có lãi hôm qua không được tính vào lãi/lỗ hôm nay.
        var sellToday = new Trade("port-1", "VNM", TradeType.SELL, 100, 18_000m, 0, 0, Today.AddHours(10));
        SetupBudget(
            allTrades: new[]
            {
                new Trade("port-1", "VNM", TradeType.BUY, 200, 20_000m, 0, 0, Today.AddDays(-10)),
                new Trade("port-1", "VNM", TradeType.SELL, 100, 25_000m, 0, 0, Today.AddDays(-1)),
                sellToday
            },
            tradesToday: new[] { sellToday });

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.DailyPnl.Should().Be(-200_000m); // (18.000 − 20.000) × 100
    }

    [Fact]
    public async Task CheckRiskBudget_SubtractsFeeAndTax()
    {
        var sell = new Trade("port-1", "VNM", TradeType.SELL, 100, 20_000m, 5_000m, 3_000m, Today.AddHours(10));
        SetupBudget(
            allTrades: new[]
            {
                new Trade("port-1", "VNM", TradeType.BUY, 100, 20_000m, 0, 0, Today.AddDays(-3)),
                sell
            },
            tradesToday: new[] { sell });

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.DailyPnl.Should().Be(-8_000m);
    }

    // ─── CalculateStressTestAsync ────────────────────────────────────────

    [Fact]
    public async Task StressTest_UsesQuantityIncludingSharesFromCorporateActions()
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("port-1", "HPG", TradeType.BUY, 100, 30_000m, 0, 0, Today.AddDays(-60))
            });
        SetupActions(CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, Today.AddDays(-30), Today.AddDays(-10)));
        _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "HPG")))
            .ReturnsAsync(new Money(23_000m));
        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData { Indicators = new FinanceIndicators { Beta = 1.0m } });

        var result = await _sut.CalculateStressTestAsync("port-1", -10m);

        // 130 CP × 23.000 = 2.990.000 (không phải 100 × 23.000 = 2.300.000)
        result.Positions.Should().ContainSingle();
        result.Positions[0].MarketValue.Should().Be(2_990_000m);
        result.TotalImpact.Should().Be(-299_000m);
    }

    [Fact]
    public async Task StressTest_IncludesSharesNotYetSettled()
    {
        // Chưa về tài khoản nhưng giá thị trường đã điều chỉnh → vẫn phải tính vào giá trị chịu rủi ro.
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("port-1", "HPG", TradeType.BUY, 100, 30_000m, 0, 0, Today.AddDays(-60))
            });
        SetupActions(CorporateAction.StockDividend(
            "port-1", "user-1", "HPG", 10, 13, Today.AddDays(-5), Today.AddDays(30)));
        _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "HPG")))
            .ReturnsAsync(new Money(23_000m));
        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData { Indicators = new FinanceIndicators { Beta = 1.0m } });

        var result = await _sut.CalculateStressTestAsync("port-1", -10m);

        result.Positions[0].MarketValue.Should().Be(2_990_000m);
    }
}
