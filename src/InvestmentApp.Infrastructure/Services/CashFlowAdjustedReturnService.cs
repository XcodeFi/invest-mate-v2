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

        decimal twr = 1m;

        for (int i = 1; i < snapshots.Count; i++)
        {
            var prevValue = snapshots[i - 1].TotalValue;
            var currentValue = snapshots[i].TotalValue;

            if (prevValue < MinSnapshotValue)
            {
                _logger.LogWarning(
                    "Skipping TWR period for portfolio {PortfolioId}: prevValue={PrevValue} below threshold {Threshold}",
                    portfolioId, prevValue, MinSnapshotValue);
                continue;
            }

            // Sum cash flows between two snapshot dates
            var periodFlows = flows
                .Where(f => f.FlowDate > snapshots[i - 1].SnapshotDate && f.FlowDate <= snapshots[i].SnapshotDate)
                .Sum(f => f.SignedAmount);

            var periodReturn = (currentValue - prevValue - periodFlows) / prevValue;

            if (Math.Abs(periodReturn) > MaxAbsPeriodReturn)
            {
                _logger.LogWarning(
                    "Skipping TWR period for portfolio {PortfolioId}: |periodReturn|={PeriodReturn} exceeds cap {Cap}",
                    portfolioId, periodReturn, MaxAbsPeriodReturn);
                continue;
            }

            twr *= (1 + periodReturn);
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
