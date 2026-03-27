using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class RiskCalculationServiceStressTestTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<IStockPriceService> _stockPriceService;
    private readonly Mock<IStopLossTargetRepository> _stopLossRepo;
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo;
    private readonly Mock<IStockPriceRepository> _stockPriceRepo;
    private readonly Mock<IPnLService> _pnlService;
    private readonly Mock<ICapitalFlowRepository> _capitalFlowRepo;
    private readonly Mock<IRiskProfileRepository> _riskProfileRepo;
    private readonly Mock<IFundamentalDataProvider> _fundamentalDataProvider;
    private readonly Mock<IComprehensiveStockDataProvider> _comprehensiveProvider;
    private readonly Mock<IMarketDataProvider> _marketDataProvider;
    private readonly Mock<ILogger<RiskCalculationService>> _logger;
    private readonly RiskCalculationService _sut;

    public RiskCalculationServiceStressTestTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _stockPriceService = new Mock<IStockPriceService>();
        _stopLossRepo = new Mock<IStopLossTargetRepository>();
        _snapshotRepo = new Mock<IPortfolioSnapshotRepository>();
        _stockPriceRepo = new Mock<IStockPriceRepository>();
        _pnlService = new Mock<IPnLService>();
        _capitalFlowRepo = new Mock<ICapitalFlowRepository>();
        _riskProfileRepo = new Mock<IRiskProfileRepository>();
        _fundamentalDataProvider = new Mock<IFundamentalDataProvider>();
        _comprehensiveProvider = new Mock<IComprehensiveStockDataProvider>();
        _marketDataProvider = new Mock<IMarketDataProvider>();
        _logger = new Mock<ILogger<RiskCalculationService>>();

        _sut = new RiskCalculationService(
            _portfolioRepo.Object,
            _tradeRepo.Object,
            _stockPriceService.Object,
            _stopLossRepo.Object,
            _snapshotRepo.Object,
            _stockPriceRepo.Object,
            _pnlService.Object,
            _capitalFlowRepo.Object,
            _riskProfileRepo.Object,
            _fundamentalDataProvider.Object,
            _comprehensiveProvider.Object,
            _marketDataProvider.Object,
            _logger.Object);
    }

    [Fact]
    public async Task StressTest_WithKnownBeta_CalculatesCorrectImpact()
    {
        // Arrange: 1 position, beta=1.2, marketValue=10M, market change=-10%
        SetupPortfolioWithPositions("port-1", new[]
        {
            ("VNM", 100m, 100_000m, 10_000_000m) // qty, price, marketValue
        });

        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Indicators = new FinanceIndicators { Beta = 1.2m }
            });

        // Act
        var result = await _sut.CalculateStressTestAsync("port-1", -10m);

        // Assert: impact = 10M * (-10/100) * 1.2 = -1.2M
        result.Positions.Should().HaveCount(1);
        result.Positions[0].Beta.Should().Be(1.2m);
        result.Positions[0].Impact.Should().Be(-1_200_000m);
        result.TotalImpact.Should().Be(-1_200_000m);
    }

    [Fact]
    public async Task StressTest_BetaFromApiFails_FallbackTo1()
    {
        SetupPortfolioWithPositions("port-1", new[]
        {
            ("FPT", 200m, 80_000m, 16_000_000m)
        });

        // API returns null beta → fallback to 1.0
        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Indicators = new FinanceIndicators { Beta = null }
            });

        // No price history for correlation fallback
        _marketDataProvider.Setup(m => m.GetHistoricalPricesAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPriceData>());

        var result = await _sut.CalculateStressTestAsync("port-1", -10m);

        // Fallback beta=1.0 → impact = 16M * (-10/100) * 1.0 = -1.6M
        result.Positions[0].Beta.Should().Be(1.0m);
        result.Positions[0].Impact.Should().Be(-1_600_000m);
    }

    [Fact]
    public async Task StressTest_NegativeChange_NegativeImpactForPositiveBeta()
    {
        SetupPortfolioWithPositions("port-1", new[]
        {
            ("VNM", 100m, 100_000m, 10_000_000m)
        });

        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Indicators = new FinanceIndicators { Beta = 1.0m }
            });

        var result = await _sut.CalculateStressTestAsync("port-1", -20m);

        result.TotalImpact.Should().BeNegative();
        result.TotalImpactPercent.Should().BeNegative();
    }

    [Fact]
    public async Task StressTest_PositiveChange_PositiveImpact()
    {
        SetupPortfolioWithPositions("port-1", new[]
        {
            ("VNM", 100m, 100_000m, 10_000_000m)
        });

        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Indicators = new FinanceIndicators { Beta = 1.0m }
            });

        var result = await _sut.CalculateStressTestAsync("port-1", 15m);

        result.TotalImpact.Should().BePositive();
    }

    [Fact]
    public async Task StressTest_MultiplePositions_SumsTotalImpact()
    {
        SetupPortfolioWithPositions("port-1", new[]
        {
            ("VNM", 100m, 100_000m, 10_000_000m),
            ("FPT", 200m, 80_000m, 16_000_000m)
        });

        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData { Indicators = new FinanceIndicators { Beta = 1.0m } });
        _comprehensiveProvider.Setup(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData { Indicators = new FinanceIndicators { Beta = 1.5m } });

        var result = await _sut.CalculateStressTestAsync("port-1", -10m);

        // VNM: 10M * -0.1 * 1.0 = -1M, FPT: 16M * -0.1 * 1.5 = -2.4M
        result.TotalImpact.Should().Be(-3_400_000m);
        result.Positions.Should().HaveCount(2);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void SetupPortfolioWithPositions(string portfolioId, (string symbol, decimal qty, decimal price, decimal marketValue)[] positions)
    {
        var trades = new List<Trade>();
        foreach (var (symbol, qty, price, _) in positions)
        {
            trades.Add(new Trade(portfolioId, symbol, TradeType.BUY, qty, price, 0, 0, DateTime.UtcNow.AddDays(-30)));
        }
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        foreach (var (symbol, _, price, _) in positions)
        {
            _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == symbol)))
                .ReturnsAsync(new Money(price));
        }
    }
}
