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

        // Outlier @01-02 is skipped (return > 500% cap). The next valid period
        // must use the pre-outlier baseline (01-01, 100M) — NOT the outlier
        // value — so 01-01 → 01-30 ⇒ +5% emerges as a clean signal.
        result.Should().BeApproximately(5m, 0.5m);
    }

    [Fact]
    public async Task CalculateTWR_FlowDuringSkippedFirstPeriod_AttributedToNextValidPeriod()
    {
        // Discovered via prod data (user truong.pham@evizi.com): a corrupt
        // first snapshot (V₀ < MinSnapshotValue, here negative) caused period
        // 0→1 to be skipped, and the deposit that happened during that
        // window was lost. The next period then read the natural value-jump
        // (which actually reflects the deposit) as a fake +137% return.
        //
        // Fix invariant: when a period is skipped (corrupt baseline OR outlier
        // capped), the flow window of the next valid period must extend back
        // to the LAST GOOD snapshot date — never advance lastValidDate on skip.
        SetupPortfolio(createdAt: new DateTime(2026, 1, 1));
        var deposit = new CapitalFlow(PortfolioId, "user-1", CapitalFlowType.Deposit, 200_000_000m,
            flowDate: new DateTime(2026, 1, 5)); // inside the skipped (01-01, 01-15] period
        SetupFlows(deposit);
        SetupSnapshots(
            (new DateTime(2026, 1, 1), -10_000_000m),   // corrupt — skipped by MinSnapshotValue guard
            (new DateTime(2026, 1, 15), 145_000_000m),  // post-deposit baseline
            (new DateTime(2026, 2, 15), 345_000_000m)); // +200M jump should ALL be flow, not return

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        // Without the fix: period (01-15, 02-15] reads (345−145−0)/145 ≈ +138%.
        // With the fix: period (01-01, 02-15] reads (345−145−200)/145 ≈ 0%.
        Math.Abs(result).Should().BeLessThan(5m,
            "the 200M flow must still be attributed despite the skipped first period");
    }

    [Fact]
    public async Task CalculateTWR_FlowOnCorruptSnapshotDate_StillAttributed()
    {
        // Tightest reproduction of the truong.pham@evizi.com prod incident.
        // Day-0 deposit (e.g. payroll funding on portfolio creation day) has
        // `FlowDate == snapshots[0].SnapshotDate` AND that snapshot is
        // corrupt (V<MinSnapshotValue). The half-open `> snap[0].date`
        // filter would drop the flow entirely; with the boundary-inclusive
        // window for the first valid period, the deposit IS captured.
        SetupPortfolio(createdAt: new DateTime(2026, 3, 9));
        var deposit = new CapitalFlow(PortfolioId, "user-1", CapitalFlowType.Deposit, 200_000_000m,
            flowDate: new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc)); // EXACT same instant as snap[0]
        SetupFlows(deposit);
        SetupSnapshots(
            (new DateTime(2026, 3, 9), -39_900_000m),    // corrupt (matches the prod data point)
            (new DateTime(2026, 3, 11), 145_000_000m),
            (new DateTime(2026, 4, 26), 345_000_000m));

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        // Without the boundary fix: flow on 03-09 fails `> 03-09` and is
        // attributed to no period → next period reads (345−145−0)/145 ≈
        // +138%. With the fix, first valid period captures the flow →
        // periodReturn ≈ 0%.
        Math.Abs(result).Should().BeLessThan(5m,
            "the day-0 deposit must be captured even when flow date == corrupt snapshot date");
    }

    [Fact]
    public async Task CalculateTWR_OutlierPeriod_FlowInOutlierPeriod_AttributedToNextPeriod()
    {
        // Same invariant for the outlier-cap skip path. A flow in a capped
        // period must not vanish — the next period's window must absorb it.
        SetupPortfolio(createdAt: new DateTime(2026, 1, 1));
        var deposit = new CapitalFlow(PortfolioId, "user-1", CapitalFlowType.Deposit, 50_000_000m,
            flowDate: new DateTime(2026, 1, 5));
        SetupFlows(deposit);
        SetupSnapshots(
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 10), 100_000_000_000m), // outlier — capped
            (new DateTime(2026, 1, 30), 155_000_000m));    // 100M + 50M deposit + 5M growth

        var result = await _sut.CalculateTWRAsync(PortfolioId);

        // Outlier (01-01, 01-10] skipped. With the fix: period (01-01, 01-30]
        // includes the 50M flow → return = (155 − 100 − 50)/100 = +5%.
        result.Should().BeApproximately(5m, 0.5m);
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

    // ─── Household tests ─────────────────────────────────────────────────

    private const string UserId = "user-1";

    private Portfolio MakePortfolio(string id, decimal initialCapital, DateTime createdAt)
    {
        var p = new Portfolio(UserId, $"PF-{id}", initialCapital);
        typeof(Portfolio).GetProperty("Id")!.SetValue(p, id);
        typeof(Portfolio).GetProperty("CreatedAt")!.SetValue(p, createdAt);
        return p;
    }

    private void SetupPortfolioSnapshots(string portfolioId, params (DateTime date, decimal totalValue)[] points)
    {
        var list = points.Select(p => new PortfolioSnapshotEntity(
            portfolioId, p.date, p.totalValue, 0, 0, 0, 0, 0, 0)).ToList();
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(
                portfolioId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private void SetupPortfolioFlows(string portfolioId, params CapitalFlow[] flows)
    {
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows.ToList());
    }

    [Fact]
    public async Task GetHouseholdReturnSummary_NoPortfolios_ReturnsEmptySummary()
    {
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio>());

        var summary = await _sut.GetHouseholdReturnSummaryAsync(UserId);

        summary.UserId.Should().Be(UserId);
        summary.PortfolioCount.Should().Be(0);
        summary.TimeWeightedReturn.Should().Be(0);
        summary.Cagr.Should().Be(0);
        summary.IsStable.Should().BeFalse();
    }

    [Fact]
    public async Task GetHouseholdReturnSummary_SinglePortfolio_MatchesPerPortfolioTwr()
    {
        // 100M → 110M → 120M ⇒ twr 20%, daysSpanned ~30 → CAGR very large (annualization)
        var p = MakePortfolio("pf-A", 100_000_000m, new DateTime(2026, 1, 1));
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { p });
        SetupPortfolioSnapshots("pf-A",
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 15), 110_000_000m),
            (new DateTime(2026, 1, 31), 120_000_000m));
        SetupPortfolioFlows("pf-A");

        var summary = await _sut.GetHouseholdReturnSummaryAsync(UserId);

        summary.PortfolioCount.Should().Be(1);
        summary.TimeWeightedReturn.Should().BeApproximately(20m, 0.01m);
        summary.TotalValue.Should().Be(120_000_000m);
        summary.DaysSpanned.Should().Be(30);
        summary.IsStable.Should().BeFalse(); // < 1 year
    }

    [Fact]
    public async Task GetHouseholdReturnSummary_TwoAlignedPortfolios_AggregatesCorrectly()
    {
        // P1 100M → 110M (+10%); P2 200M → 210M (+5%) on same dates.
        // Aggregate: 300M → 320M ⇒ twr ~6.67%
        var p1 = MakePortfolio("pf-A", 100_000_000m, new DateTime(2026, 1, 1));
        var p2 = MakePortfolio("pf-B", 200_000_000m, new DateTime(2026, 1, 1));
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { p1, p2 });
        SetupPortfolioSnapshots("pf-A",
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 31), 110_000_000m));
        SetupPortfolioSnapshots("pf-B",
            (new DateTime(2026, 1, 1), 200_000_000m),
            (new DateTime(2026, 1, 31), 210_000_000m));
        SetupPortfolioFlows("pf-A");
        SetupPortfolioFlows("pf-B");

        var summary = await _sut.GetHouseholdReturnSummaryAsync(UserId);

        summary.PortfolioCount.Should().Be(2);
        summary.TimeWeightedReturn.Should().BeApproximately(6.6667m, 0.01m);
        summary.TotalValue.Should().Be(320_000_000m);
    }

    [Fact]
    public async Task GetHouseholdReturnSummary_LateJoiningPortfolio_NotInflateReturn()
    {
        // P1 alone day 1: 100M. Day 15: P2 joins with 200M, P1 still 100M ⇒
        // aggregate jumps 100M → 300M. Without flow attribution, periodReturn
        // would be +200% (huge fake CAGR). With synthetic flow=200M on day 15,
        // periodReturn = (300 − 100 − 200)/100 = 0% ⇒ aggregate TWR ≈ 0.
        var p1 = MakePortfolio("pf-A", 100_000_000m, new DateTime(2026, 1, 1));
        var p2 = MakePortfolio("pf-B", 200_000_000m, new DateTime(2026, 1, 15));
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { p1, p2 });
        SetupPortfolioSnapshots("pf-A",
            (new DateTime(2026, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 15), 100_000_000m));
        SetupPortfolioSnapshots("pf-B",
            (new DateTime(2026, 1, 15), 200_000_000m));
        SetupPortfolioFlows("pf-A");
        SetupPortfolioFlows("pf-B");

        var summary = await _sut.GetHouseholdReturnSummaryAsync(UserId);

        summary.TimeWeightedReturn.Should().BeApproximately(0m, 0.01m);
        summary.TotalValue.Should().Be(300_000_000m);
    }

    [Fact]
    public async Task GetHouseholdReturnSummary_OneYearWindow_MarksStable()
    {
        // 365 days → years = 1.0 ⇒ IsStable = true
        var p = MakePortfolio("pf-A", 100_000_000m, new DateTime(2025, 1, 1));
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { p });
        SetupPortfolioSnapshots("pf-A",
            (new DateTime(2025, 1, 1), 100_000_000m),
            (new DateTime(2026, 1, 1), 110_000_000m));
        SetupPortfolioFlows("pf-A");

        var summary = await _sut.GetHouseholdReturnSummaryAsync(UserId);

        summary.DaysSpanned.Should().Be(365);
        summary.IsStable.Should().BeTrue();
    }
}
