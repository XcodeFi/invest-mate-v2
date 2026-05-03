using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Calculates cash-flow adjusted returns using TWR and MWR methods.
/// TWR eliminates the effect of external cash flows.
/// MWR (XIRR) measures the investor's actual return including timing of flows.
/// </summary>
public class CashFlowAdjustedReturnService : ICashFlowAdjustedReturnService
{
    // Below this prev-value threshold the period return formula
    // (V_i - V_{i-1} - C_i) / V_{i-1} becomes unstable: a bad/near-zero
    // snapshot would produce astronomical period returns and corrupt the
    // product chain. Skip such periods.
    private const decimal MinSnapshotValue = 1000m;

    // Extreme single-period returns (>500% or <-95%) are almost always a
    // data issue (snapshot glitch, missed flow attribution), not a real
    // one-day move. Skip so one outlier doesn't wreck the whole TWR.
    private const decimal MaxAbsPeriodReturn = 5m;

    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPnLService _pnlService;
    private readonly ITradeRepository _tradeRepository;
    private readonly ILogger<CashFlowAdjustedReturnService> _logger;

    public CashFlowAdjustedReturnService(
        ICapitalFlowRepository capitalFlowRepository,
        IPortfolioRepository portfolioRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IPnLService pnlService,
        ITradeRepository tradeRepository,
        ILogger<CashFlowAdjustedReturnService> logger)
    {
        _capitalFlowRepository = capitalFlowRepository;
        _portfolioRepository = portfolioRepository;
        _snapshotRepository = snapshotRepository;
        _pnlService = pnlService;
        _tradeRepository = tradeRepository;
        _logger = logger;
    }

    /// <summary>
    /// TWR = Π(1 + Ri) - 1 where Ri = (Vi - Vi-1 - Ci) / Vi-1
    /// </summary>
    public async Task<decimal> CalculateTWRAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null) return 0;

        // Seed deposits represent initial funding (baseline) and are handled
        // separately by the formulas below; treating them as external cash
        // flows would distort period returns.
        var flows = (await _capitalFlowRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken))
            .Where(f => !f.IsSeedDeposit).ToList();
        var snapshots = (await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId,
            portfolio.CreatedAt.Date,
            DateTime.UtcNow.Date,
            cancellationToken)).OrderBy(s => s.SnapshotDate).ToList();

        if (snapshots.Count < 2) return 0;

        // Track the last snapshot we *successfully* used as a return baseline.
        // Critical: when a period is skipped (corrupt prev OR outlier-capped),
        // we keep `lastValidDate` pointing at the older boundary so the next
        // valid period's flow window extends back across the gap. Otherwise a
        // deposit that landed inside a skipped period vanishes, and the value
        // jump it caused is misread as +N00% return.
        var lastValidDate = snapshots[0].SnapshotDate;
        var lastValidValue = snapshots[0].TotalValue;
        // Until we successfully use a snapshot as a baseline, treat the lower
        // flow bound as inclusive (>=) so flows on the corrupt-baseline date
        // itself (e.g. day-0 funding deposit before the snapshot ran) are
        // captured in the first valid period.
        var baselineEstablished = lastValidValue >= MinSnapshotValue;
        decimal twr = 1m;

        for (int i = 1; i < snapshots.Count; i++)
        {
            var currentValue = snapshots[i].TotalValue;

            if (lastValidValue < MinSnapshotValue)
            {
                // No usable baseline yet. Adopt the first good value we see —
                // but DON'T advance lastValidDate, so flows that fell inside
                // the corrupt span carry over into the next computed period.
                _logger.LogWarning(
                    "Skipping TWR period for portfolio {PortfolioId}: lastValidValue={LastValue} below threshold {Threshold}",
                    portfolioId, lastValidValue, MinSnapshotValue);
                if (currentValue >= MinSnapshotValue)
                {
                    lastValidValue = currentValue;
                }
                continue;
            }

            var periodFlows = flows
                .Where(f => (baselineEstablished ? f.FlowDate > lastValidDate : f.FlowDate >= lastValidDate)
                            && f.FlowDate <= snapshots[i].SnapshotDate)
                .Sum(f => f.SignedAmount);

            var periodReturn = (currentValue - lastValidValue - periodFlows) / lastValidValue;

            if (Math.Abs(periodReturn) > MaxAbsPeriodReturn)
            {
                // Outlier-capped: do NOT advance baseline. Keep lastValidValue
                // and lastValidDate so the next period's window absorbs any
                // flow that landed inside this capped span.
                _logger.LogWarning(
                    "Skipping TWR period for portfolio {PortfolioId}: |periodReturn|={PeriodReturn} exceeds cap {Cap}",
                    portfolioId, periodReturn, MaxAbsPeriodReturn);
                continue;
            }

            twr *= (1 + periodReturn);
            lastValidValue = currentValue;
            lastValidDate = snapshots[i].SnapshotDate;
            baselineEstablished = true;
        }

        return Math.Round((twr - 1) * 100, 4); // Return as percentage
    }

    /// <summary>
    /// MWR (Money-Weighted Return) using Newton-Raphson approximation.
    /// Solves: 0 = -C0 + Σ(Ci / (1+r)^ti) + VN / (1+r)^N
    /// </summary>
    public async Task<decimal> CalculateMWRAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null) return 0;

        // Exclude seed: NPV equation uses `-portfolio.InitialCapital` as the
        // t=0 outflow; a seed Deposit would double-count it.
        var flows = (await _capitalFlowRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken))
            .Where(f => !f.IsSeedDeposit)
            .OrderBy(f => f.FlowDate).ToList();

        var currentValue = await ComputeCurrentValueAsync(portfolio, flows, cancellationToken);

        // Build cash flow timeline
        var startDate = portfolio.CreatedAt.Date;
        var endDate = DateTime.UtcNow.Date;
        var totalDays = (endDate - startDate).TotalDays;

        if (totalDays <= 0) return 0;

        // Newton-Raphson to find IRR
        decimal rate = 0.1m; // Initial guess: 10%
        const int maxIterations = 100;
        const decimal tolerance = 0.0001m;
        bool converged = false;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            decimal npv = -portfolio.InitialCapital;
            decimal dnpv = 0;

            foreach (var flow in flows)
            {
                var t = (decimal)(flow.FlowDate - startDate).TotalDays / 365.25m;
                var factor = (decimal)Math.Pow((double)(1 + rate), (double)t);
                if (factor == 0) continue;

                npv -= flow.SignedAmount / factor;
                dnpv += flow.SignedAmount * t / (factor * (1 + rate));
            }

            // Add terminal value
            var tEnd = (decimal)totalDays / 365.25m;
            var endFactor = (decimal)Math.Pow((double)(1 + rate), (double)tEnd);
            if (endFactor != 0)
            {
                npv += currentValue / endFactor;
                dnpv -= currentValue * tEnd / (endFactor * (1 + rate));
            }

            if (dnpv == 0) break;

            var newRate = rate - npv / dnpv;
            if (Math.Abs(newRate - rate) < tolerance)
            {
                rate = newRate;
                converged = true;
                break;
            }
            rate = newRate;

            // Diverged: bail out rather than return garbage.
            if (rate < -0.999m || rate > 100m)
            {
                _logger.LogWarning(
                    "MWR Newton-Raphson diverged for portfolio {PortfolioId} at iteration {Iteration}, rate={Rate}",
                    portfolioId, iteration, rate);
                return 0;
            }
        }

        if (!converged)
        {
            _logger.LogWarning(
                "MWR Newton-Raphson did not converge for portfolio {PortfolioId} within {MaxIterations} iterations",
                portfolioId, maxIterations);
        }

        return Math.Round(rate * 100, 4); // Return as percentage
    }

    public async Task<AdjustedReturnSummary> GetAdjustedReturnSummaryAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null)
        {
            return new AdjustedReturnSummary { PortfolioId = portfolioId };
        }

        // Exclude seed: aggregates represent "user activity after creation";
        // cashBalance formula `InitialCapital + netFlow` also requires exclusion.
        var flows = (await _capitalFlowRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken))
            .Where(f => !f.IsSeedDeposit).ToList();

        var twr = await CalculateTWRAsync(portfolioId, cancellationToken);
        var mwr = await CalculateMWRAsync(portfolioId, cancellationToken);

        var totalDeposits = flows.Where(f => f.Type == CapitalFlowType.Deposit || f.Type == CapitalFlowType.Dividend || f.Type == CapitalFlowType.Interest)
            .Sum(f => f.Amount);
        var totalWithdrawals = flows.Where(f => f.Type == CapitalFlowType.Withdraw || f.Type == CapitalFlowType.Fee)
            .Sum(f => f.Amount);

        var currentValue = await ComputeCurrentValueAsync(portfolio, flows, cancellationToken);

        return new AdjustedReturnSummary
        {
            PortfolioId = portfolioId,
            TimeWeightedReturn = twr,
            MoneyWeightedReturn = mwr,
            TotalDeposits = totalDeposits,
            TotalWithdrawals = totalWithdrawals,
            NetCashFlow = totalDeposits - totalWithdrawals,
            CurrentValue = currentValue,
            FlowCount = flows.Count
        };
    }

    public async Task<HouseholdReturnSummary> GetHouseholdReturnSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var portfolios = (await _portfolioRepository.GetByUserIdAsync(userId, cancellationToken)).ToList();
        if (portfolios.Count == 0)
        {
            return new HouseholdReturnSummary { UserId = userId };
        }

        // Per-portfolio inputs (snapshots ordered, flows excluding seed).
        var snapshotsByPortfolio = new Dictionary<string, List<PortfolioSnapshotEntity>>();
        var allFlows = new List<(DateTime FlowDate, decimal SignedAmount)>();
        foreach (var p in portfolios)
        {
            var snaps = (await _snapshotRepository.GetByPortfolioIdAsync(
                p.Id, p.CreatedAt.Date, DateTime.UtcNow.Date, cancellationToken))
                .OrderBy(s => s.SnapshotDate).ToList();
            snapshotsByPortfolio[p.Id] = snaps;

            var flows = (await _capitalFlowRepository.GetByPortfolioIdAsync(p.Id, cancellationToken))
                .Where(f => !f.IsSeedDeposit);
            foreach (var f in flows)
                allFlows.Add((f.FlowDate, f.SignedAmount));
        }

        // Union of all snapshot dates across portfolios.
        var unionDates = snapshotsByPortfolio.Values
            .SelectMany(list => list.Select(s => s.SnapshotDate))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (unionDates.Count < 2)
        {
            // Not enough series points to compute a TWR.
            return new HouseholdReturnSummary
            {
                UserId = userId,
                PortfolioCount = portfolios.Count,
                FirstSnapshotDate = unionDates.FirstOrDefault(),
                LastSnapshotDate = unionDates.LastOrDefault()
            };
        }

        // Aggregate value per date = Σ(latest snapshot of each portfolio with date ≤ d).
        // Carry-forward keeps the aggregate stable when one portfolio misses a day.
        var aggregateByDate = new Dictionary<DateTime, decimal>(unionDates.Count);
        foreach (var d in unionDates)
        {
            decimal sum = 0;
            foreach (var (pid, snaps) in snapshotsByPortfolio)
            {
                // Latest snapshot for portfolio pid with SnapshotDate ≤ d (binary search-ish).
                PortfolioSnapshotEntity? last = null;
                foreach (var s in snaps)
                {
                    if (s.SnapshotDate <= d) last = s;
                    else break; // snaps already ordered ascending
                }
                if (last != null) sum += last.TotalValue;
            }
            aggregateByDate[d] = sum;
        }

        // Synthetic flows: when a portfolio first contributes to the aggregate
        // (first snapshot AFTER the first union date), its first-snapshot
        // TotalValue is treated as an inflow on that date — otherwise the
        // jump from "P2 not yet in aggregate" to "P2 in aggregate" would be
        // misread as a huge return.
        var firstUnionDate = unionDates[0];
        var syntheticFlows = new List<(DateTime FlowDate, decimal SignedAmount)>();
        foreach (var (pid, snaps) in snapshotsByPortfolio)
        {
            if (snaps.Count == 0) continue;
            var firstSnap = snaps[0];
            if (firstSnap.SnapshotDate > firstUnionDate)
                syntheticFlows.Add((firstSnap.SnapshotDate, firstSnap.TotalValue));
        }

        var combinedFlows = allFlows.Concat(syntheticFlows).ToList();

        // TWR loop on aggregate series — same `lastValidDate / lastValidValue /
        // baselineEstablished` pattern as per-portfolio TWR (v2.53.1) so a flow
        // landing on or inside a skipped/corrupt aggregate snapshot still gets
        // attributed to the next valid period.
        var lastValidDate = unionDates[0];
        var lastValidValue = aggregateByDate[unionDates[0]];
        var baselineEstablished = lastValidValue >= MinSnapshotValue;
        decimal twr = 1m;

        for (int i = 1; i < unionDates.Count; i++)
        {
            var currentValue = aggregateByDate[unionDates[i]];

            if (lastValidValue < MinSnapshotValue)
            {
                _logger.LogWarning(
                    "Skipping household TWR period for user {UserId}: lastValidValue={LastValue} below threshold {Threshold}",
                    userId, lastValidValue, MinSnapshotValue);
                if (currentValue >= MinSnapshotValue)
                {
                    lastValidValue = currentValue;
                    // Don't advance lastValidDate so flows in the skipped span carry over.
                }
                continue;
            }

            var periodFlows = combinedFlows
                .Where(f => (baselineEstablished ? f.FlowDate > lastValidDate : f.FlowDate >= lastValidDate)
                            && f.FlowDate <= unionDates[i])
                .Sum(f => f.SignedAmount);

            var periodReturn = (currentValue - lastValidValue - periodFlows) / lastValidValue;

            if (Math.Abs(periodReturn) > MaxAbsPeriodReturn)
            {
                _logger.LogWarning(
                    "Skipping household TWR period for user {UserId}: |periodReturn|={PeriodReturn} exceeds cap {Cap}",
                    userId, periodReturn, MaxAbsPeriodReturn);
                continue;
            }

            twr *= (1 + periodReturn);
            lastValidValue = currentValue;
            lastValidDate = unionDates[i];
            baselineEstablished = true;
        }

        var twrPct = Math.Round((twr - 1) * 100, 4);

        // Annualize. < ~30 days → CAGR not meaningful; leave at 0.
        var firstDate = unionDates[0];
        var lastDate = unionDates[^1];
        var daysSpanned = (int)Math.Round((lastDate - firstDate).TotalDays);
        var years = daysSpanned / 365.25;
        decimal cagr = 0;
        if (years >= 0.08)
        {
            var twrFraction = (double)twrPct / 100.0;
            if (twrFraction > -1)
            {
                var annualized = (decimal)(Math.Pow(1 + twrFraction, 1.0 / years) - 1) * 100;
                if (!double.IsInfinity((double)annualized) && !double.IsNaN((double)annualized))
                    cagr = Math.Round(Math.Clamp(annualized, -99.99m, 9999.99m), 2);
            }
        }

        return new HouseholdReturnSummary
        {
            UserId = userId,
            PortfolioCount = portfolios.Count,
            TotalValue = aggregateByDate[lastDate],
            TimeWeightedReturn = twrPct,
            Cagr = cagr,
            FirstSnapshotDate = firstDate,
            LastSnapshotDate = lastDate,
            DaysSpanned = daysSpanned,
            IsStable = daysSpanned >= 365
        };
    }

    /// <summary>
    /// currentValue = cashBalance + marketValue where
    ///   cashBalance = InitialCapital + netFlow - gross_buys + gross_sells
    /// Uses gross historical trade amounts (not PnL.TotalInvested which only
    /// reflects open positions) — matches the capital-flows hero card math.
    /// </summary>
    private async Task<decimal> ComputeCurrentValueAsync(
        Portfolio portfolio,
        IReadOnlyCollection<CapitalFlow> flowsExcludingSeed,
        CancellationToken cancellationToken)
    {
        try
        {
            var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolio.Id, cancellationToken);
            var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
            var tradeList = trades.ToList();
            var grossBuys = tradeList.Where(t => t.TradeType == TradeType.BUY)
                .Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);
            var grossSells = tradeList.Where(t => t.TradeType == TradeType.SELL)
                .Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);
            var netFlow = flowsExcludingSeed.Sum(f => f.SignedAmount);
            var cashBalance = portfolio.InitialCapital + netFlow - grossBuys + grossSells;
            return pnl.TotalPortfolioValue + cashBalance;
        }
        catch
        {
            return portfolio.InitialCapital + flowsExcludingSeed.Sum(f => f.SignedAmount);
        }
    }
}
