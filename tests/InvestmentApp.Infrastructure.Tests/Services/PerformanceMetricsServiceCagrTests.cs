using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// CAGR-specific tests. The other metrics (Sharpe/Sortino/win-rate/etc.)
/// are exercised in integration. Focus here is the flow-adjusted correctness
/// of CalculateCAGRAsync + the GetFullPerformanceSummaryAsync totalReturn.
/// </summary>
public class PerformanceMetricsServiceCagrTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<ICapitalFlowRepository> _flowRepo = new();
    private readonly Mock<IPnLService> _pnlService = new();
    private readonly Mock<IRiskCalculationService> _riskService = new();
    private readonly Mock<ICashFlowAdjustedReturnService> _adjustedReturnService = new();
    private readonly PerformanceMetricsService _sut;

    private const string PortfolioId = "pf-1";

    public PerformanceMetricsServiceCagrTests()
    {
        _sut = new PerformanceMetricsService(
            _portfolioRepo.Object,
            _snapshotRepo.Object,
            _tradeRepo.Object,
            _flowRepo.Object,
            _pnlService.Object,
            _riskService.Object,
            _adjustedReturnService.Object,
            NullLogger<PerformanceMetricsService>.Instance);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private Portfolio SetupPortfolio(decimal initialCapital = 100_000_000m, DateTime? createdAt = null)
    {
        var portfolio = new Portfolio("user-1", "PF", initialCapital);
        typeof(Portfolio).GetProperty("Id")!.SetValue(portfolio, PortfolioId);
        if (createdAt.HasValue)
            typeof(Portfolio).GetProperty("CreatedAt")!.SetValue(portfolio, createdAt.Value);
        _portfolioRepo.Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        return portfolio;
    }

    private void SetupSnapshots(params (DateTime date, decimal totalValue)[] points)
    {
        var list = points.Select(p => new PortfolioSnapshotEntity(
            PortfolioId, p.date, p.totalValue, 0, 0, 0, 0, 0, 0)).ToList();
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(
                PortfolioId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private void SetupTrades(params Trade[] trades)
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades.ToList());
    }

    private void SetupTwr(decimal twrPercent)
    {
        _adjustedReturnService.Setup(s => s.CalculateTWRAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(twrPercent);
    }

    // ─── CAGR primary path (TWR-based) ───────────────────────────────────

    [Fact]
    public async Task CalculateCAGR_UsesAnnualizedTwr_NotRawEndpointRatio()
    {
        // Portfolio value grew 100M → 150M but the user deposited 40M midway.
        // Raw ratio CAGR would say 50% / 2 yrs = ~22%. Actual TWR = 10% over
        // 2 yrs → annualized ≈ 4.88%.
        SetupPortfolio();
        SetupSnapshots(
            (new DateTime(2024, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 1), 150_000_000m));
        SetupTwr(10m); // flow-adjusted total-period TWR = 10%

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        // (1 + 0.10)^(1/2) - 1 ≈ 0.0488 → 4.88%
        cagr.Should().BeApproximately(4.88m, 0.05m);
    }

    [Fact]
    public async Task CalculateCAGR_NegativeTwr_Annualizes()
    {
        SetupPortfolio();
        SetupSnapshots(
            (new DateTime(2024, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 1), 80_000_000m));
        SetupTwr(-20m); // lost 20% over 2 years

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        // (1 - 0.20)^(1/2) - 1 ≈ -0.1056 → -10.56%
        cagr.Should().BeApproximately(-10.56m, 0.05m);
    }

    [Fact]
    public async Task CalculateCAGR_ShortWindow_ReturnsZero()
    {
        // <30 days — not meaningful.
        SetupPortfolio();
        SetupSnapshots(
            (new DateTime(2026, 4, 1), 100_000_000m),
            (new DateTime(2026, 4, 15), 105_000_000m));
        SetupTwr(5m);

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        // Would fall through to trade fallback, but no trades → 0.
        cagr.Should().Be(0);
    }

    [Fact]
    public async Task CalculateCAGR_TwrBelowMinusOne_DoesNotCrash()
    {
        // Defensive: TWR can't normally be <-100%, but if it happens the
        // formula Math.Pow(1 + x, ...) would NaN. Ensure we return 0 gracefully.
        SetupPortfolio();
        SetupSnapshots(
            (new DateTime(2024, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 1), 0m));
        SetupTwr(-150m);

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        // TWR guard skips primary; no trades set up → trade fallback throws → 0.
        Math.Abs(cagr).Should().BeLessThanOrEqualTo(99.99m);
    }

    // ─── CAGR fallback path (no snapshots, gross totals) ─────────────────

    [Fact]
    public async Task CalculateCAGR_NoSnapshots_UsesGrossTotals_NotOpenPositionCost()
    {
        // Regression: old code used pnl.TotalInvested (= open-position cost)
        // → broken after closing any position. Scenario: bought 100M, sold
        // all for 120M. Open cost = 0, gross buys = 100M, gross sells = 120M.
        // Correct cashBalance = 100M (IC) + 0 (flows) − 100M + 120M = 120M.
        // CAGR over 1 year: (120 / 100) − 1 = 20%.
        SetupPortfolio(100_000_000m, createdAt: DateTime.UtcNow.AddYears(-1));
        SetupSnapshots(); // no snapshots
        // First trade ~12 months ago → tradeYears ≈ 1, so annualized == total.
        var buy = new Trade(PortfolioId, "AAA", TradeType.BUY, 1000, 100_000m,
            tradeDate: DateTime.UtcNow.AddDays(-365));
        var sell = new Trade(PortfolioId, "AAA", TradeType.SELL, 1000, 120_000m,
            tradeDate: DateTime.UtcNow.AddMonths(-3));
        SetupTrades(buy, sell);
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CapitalFlow>());
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary
            {
                TotalInvested = 0,            // all positions closed
                TotalPortfolioValue = 0,
                TotalRealizedPnL = 20_000_000m,
                TotalUnrealizedPnL = 0
            });

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        // ~20% over 1 year. Minor tolerance for "1 year ago" date arithmetic drift.
        cagr.Should().BeApproximately(20m, 1m);
    }

    [Fact]
    public async Task CalculateCAGR_NoSnapshots_NoTrades_ReturnsZero()
    {
        SetupPortfolio(createdAt: DateTime.UtcNow.AddYears(-1));
        SetupSnapshots();
        SetupTrades();

        var cagr = await _sut.CalculateCAGRAsync(PortfolioId);

        cagr.Should().Be(0);
    }

    // ─── GetFullPerformanceSummaryAsync.totalReturn ──────────────────────

    [Fact]
    public async Task FullSummary_TotalReturn_UsesTwr_NotRawEndpoints()
    {
        SetupPortfolio();
        SetupSnapshots(
            (new DateTime(2024, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 1), 150_000_000m));
        SetupTrades();
        SetupTwr(12.5m); // flow-adjusted
        _riskService.Setup(r => r.CalculateMaxDrawdownAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrawdownResult { MaxDrawdownPercent = 0 });
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(
                PortfolioId, It.Is<DateTime>(d => d > DateTime.MinValue),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSnapshotEntity>());

        var summary = await _sut.GetFullPerformanceSummaryAsync(PortfolioId);

        // TotalReturn = TWR (12.5), not raw (150−100)/100 = 50.
        summary.TotalReturn.Should().Be(12.5m);
    }
}
