using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class CashFlowAdjustedReturnServiceTests
{
    private readonly Mock<ICapitalFlowRepository> _flowRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<IPnLService> _pnlService = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly ILogger<CashFlowAdjustedReturnService> _logger = NullLogger<CashFlowAdjustedReturnService>.Instance;
    private readonly CashFlowAdjustedReturnService _sut;

    private const string PortfolioId = "pf-1";

    public CashFlowAdjustedReturnServiceTests()
    {
        _sut = new CashFlowAdjustedReturnService(
            _flowRepo.Object,
            _portfolioRepo.Object,
            _snapshotRepo.Object,
            _pnlService.Object,
            _tradeRepo.Object,
            _logger);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private Portfolio SetupPortfolio(decimal initialCapital = 100_000_000m, DateTime? createdAt = null)
    {
        var portfolio = new Portfolio("user-1", "PF", initialCapital);
        typeof(Portfolio).GetProperty("Id")!.SetValue(portfolio, PortfolioId);
        if (createdAt.HasValue)
        {
            typeof(Portfolio).GetProperty("CreatedAt")!.SetValue(portfolio, createdAt.Value);
        }
        _portfolioRepo.Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        return portfolio;
    }

    private void SetupFlows(params CapitalFlow[] flows)
    {
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows.ToList());
    }

    private void SetupSnapshots(params (DateTime date, decimal totalValue)[] points)
    {
        var list = points.Select(p => new PortfolioSnapshotEntity(
            PortfolioId, p.date, p.totalValue, 0, 0, 0, 0, 0, 0)).ToList();
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(
                PortfolioId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private void SetupNoTrades()
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade>());
    }

    // ─── TWR tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateTWR_NoPortfolio_ReturnsZero()
    {
        var result = await _sut.CalculateTWRAsync(PortfolioId);
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateTWR_FewerThanTwoSnapshots_ReturnsZero()
    {
        SetupPortfolio();
        SetupFlows();
        SetupSnapshots((DateTime.UtcNow.Date, 100_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateTWR_NormalPath_NoFlows_ReturnsExpectedValue()
    {
        // 100M → 105M → 110M ⇒ twr ≈ 10%
        SetupPortfolio();
        SetupFlows();
        SetupSnapshots(
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 15), 105_000_000m),
            (new DateTime(2026, 1, 30), 110_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        result.Should().BeApproximately(10m, 0.01m);
    }

    [Fact]
    public async Task CalculateTWR_WithFlow_AdjustsForFlow()
    {
        // V0=100M, deposit +50M between, V1=155M ⇒ R1 = (155-100-50)/100 = 5%
        var portfolio = SetupPortfolio();
        var flow = new CapitalFlow(PortfolioId, "user-1", CapitalFlowType.Deposit, 50_000_000m,
            flowDate: new DateTime(2026, 1, 10));
        SetupFlows(flow);
        SetupSnapshots(
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 15), 155_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        result.Should().BeApproximately(5m, 0.01m);
    }

    [Fact]
    public async Task CalculateTWR_NearZeroSnapshot_DoesNotBlowUp()
    {
        // V0=0.5đ (bad snapshot), V1=100M — old code: periodReturn = ~2e8, twr blows up
        SetupPortfolio();
        SetupFlows();
        SetupSnapshots(
            (new DateTime(2026, 1, 1), 0.5m),
            (new DateTime(2026, 1, 2), 100_000_000m),
            (new DateTime(2026, 1, 30), 105_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        // Result must be finite and bounded (period with near-zero V_prev is skipped);
        // the clean 100M→105M period gives ~5%.
        Math.Abs(result).Should().BeLessThan(1000m);
        result.Should().BeApproximately(5m, 0.01m);
    }

    [Fact]
    public async Task CalculateTWR_OutlierPeriod_IsSkipped()
    {
        // Period return > 500% (data glitch) should be skipped so one outlier
        // doesn't corrupt the whole product chain.
        SetupPortfolio();
        SetupFlows();
        SetupSnapshots(
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 2), 100_000_000_000m), // 1000× spike (bad data)
            (new DateTime(2026, 1, 30), 105_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        // Without the cap, TWR would be (100,000× * ~0) - 1 → huge negative garbage.
        // With the cap, both outlier periods are skipped → TWR ≈ 0.
        Math.Abs(result).Should().BeLessThan(1000m);
    }

    // ─── MWR tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateMWR_NoFlows_FlatPortfolio_ReturnsApproximatelyZero()
    {
        var portfolio = SetupPortfolio(100_000_000m, createdAt: DateTime.UtcNow.AddYears(-1));
        SetupFlows();
        SetupNoTrades();
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary
            {
                TotalInvested = 0,
                TotalPortfolioValue = 0,
                TotalRealizedPnL = 0,
                TotalUnrealizedPnL = 0
            });

        var result = await _sut.CalculateMWRAsync(PortfolioId);

        // Cash balance should be 100M (InitialCapital); no P&L → rate ≈ 0.
        Math.Abs(result).Should().BeLessThan(1m);
    }

    [Fact]
    public async Task CalculateMWR_UsesGrossTradeValuesForCashBalance()
    {
        // After buy 100M → sell all for 120M: open-position cost = 0 (PnL.TotalInvested),
        // but gross bought = 100M, gross sold = 120M.
        // Correct cashBalance = 100M (initial) + 0 (flows) - 100M (gross buy) + 120M (gross sell)
        // = 120M. If we use PnL.TotalInvested=0 wrongly, cashBalance would over-report as 100M + pnl.Value.
        var portfolio = SetupPortfolio(100_000_000m, createdAt: DateTime.UtcNow.AddYears(-1));
        SetupFlows();
        var buy = new Trade(PortfolioId, "AAA", TradeType.BUY, 1000, 100_000m,
            fee: 0, tax: 0, tradeDate: DateTime.UtcNow.AddMonths(-6));
        var sell = new Trade(PortfolioId, "AAA", TradeType.SELL, 1000, 120_000m,
            fee: 0, tax: 0, tradeDate: DateTime.UtcNow.AddMonths(-3));
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { buy, sell });
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary
            {
                TotalInvested = 0, // positions all closed
                TotalPortfolioValue = 0,
                TotalRealizedPnL = 20_000_000m,
                TotalUnrealizedPnL = 0
            });

        var summary = await _sut.GetAdjustedReturnSummaryAsync(PortfolioId);

        // currentValue = cashBalance + marketValue = 120M + 0 = 120M
        summary.CurrentValue.Should().Be(120_000_000m);
    }
}
