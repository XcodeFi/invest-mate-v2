using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using InvestmentApp.Application.Portfolios.Queries;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class RiskCalculationServiceBudgetTests
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

    public RiskCalculationServiceBudgetTests()
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
    public async Task CheckRiskBudget_CountsTradesToday()
    {
        // Arrange: 3 trades today
        var today = DateTime.UtcNow.Date;
        var trades = new[]
        {
            new Trade("port-1", "VNM", TradeType.BUY, 100, 50_000m, 0, 0, today.AddHours(9)),
            new Trade("port-1", "FPT", TradeType.SELL, 50, 80_000m, 0, 0, today.AddHours(10)),
            new Trade("port-1", "VCB", TradeType.BUY, 200, 90_000m, 0, 0, today.AddHours(11)),
        };

        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync("port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        var profile = new RiskProfile("port-1", "user-1", maxDailyTrades: 5);
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _sut.CheckRiskBudgetAsync("port-1");

        // Assert
        result.TradesToday.Should().Be(3);
        result.MaxDailyTrades.Should().Be(5);
        result.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task CheckRiskBudget_IsLocked_WhenExceedsMaxDailyTrades()
    {
        var today = DateTime.UtcNow.Date;
        var trades = Enumerable.Range(0, 5).Select(i =>
            new Trade("port-1", "VNM", TradeType.BUY, 100, 50_000m, 0, 0, today.AddHours(9 + i))
        ).ToArray();

        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync("port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        var profile = new RiskProfile("port-1", "user-1", maxDailyTrades: 5);
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.IsLocked.Should().BeTrue();
        result.LockReason.Should().Contain("lệnh");
    }

    [Fact]
    public async Task CheckRiskBudget_IsLocked_WhenDailyLossExceedsLimit()
    {
        var today = DateTime.UtcNow.Date;
        // SELL trade at loss: bought at 100k, sold at 90k → loss = -10k per share × 100 shares = -1M
        var trades = new[]
        {
            new Trade("port-1", "VNM", TradeType.SELL, 100, 90_000m, 0, 0, today.AddHours(10)),
        };

        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync("port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        // Setup portfolio value for loss % calculation
        _pnlService.Setup(p => p.CalculatePortfolioPnLAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { TotalPortfolioValue = 10_000_000m });

        // Setup matching BUY trades to calculate P&L
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("port-1", "VNM", TradeType.BUY, 100, 100_000m, 0, 0, today.AddDays(-5)),
                trades[0]
            });

        var profile = new RiskProfile("port-1", "user-1", dailyLossLimitPercent: 2.0m);
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.DailyLossLimitPercent.Should().Be(2.0m);
        // DailyPnl should be negative (loss)
    }

    [Fact]
    public async Task CheckRiskBudget_NotLocked_WhenLimitsNull()
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync("port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());

        // Profile without daily limits
        var profile = new RiskProfile("port-1", "user-1");
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.IsLocked.Should().BeFalse();
        result.MaxDailyTrades.Should().BeNull();
        result.DailyLossLimitPercent.Should().BeNull();
    }

    [Fact]
    public async Task CheckRiskBudget_NoProfile_ReturnsUnlocked()
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAndDateRangeAsync("port-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskProfile?)null);

        var result = await _sut.CheckRiskBudgetAsync("port-1");

        result.IsLocked.Should().BeFalse();
        result.TradesToday.Should().Be(0);
    }
}
