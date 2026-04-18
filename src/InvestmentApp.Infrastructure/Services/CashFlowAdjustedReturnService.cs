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
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPnLService _pnlService;
    private readonly ILogger<CashFlowAdjustedReturnService> _logger;

    public CashFlowAdjustedReturnService(
        ICapitalFlowRepository capitalFlowRepository,
        IPortfolioRepository portfolioRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IPnLService pnlService,
        ILogger<CashFlowAdjustedReturnService> logger)
    {
        _capitalFlowRepository = capitalFlowRepository;
        _portfolioRepository = portfolioRepository;
        _snapshotRepository = snapshotRepository;
        _pnlService = pnlService;
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

            // Sum cash flows between two snapshot dates
            var periodFlows = flows
                .Where(f => f.FlowDate > snapshots[i - 1].SnapshotDate && f.FlowDate <= snapshots[i].SnapshotDate)
                .Sum(f => f.SignedAmount);

            if (prevValue != 0)
            {
                var periodReturn = (currentValue - prevValue - periodFlows) / prevValue;
                twr *= (1 + periodReturn);
            }
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

        // Get current portfolio value
        decimal currentValue;
        try
        {
            var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
            var totalFlows = flows.Sum(f => f.SignedAmount);
            var cashBalance = portfolio.InitialCapital + totalFlows - pnl.TotalInvested;
            currentValue = pnl.TotalPortfolioValue + cashBalance;
        }
        catch
        {
            currentValue = portfolio.InitialCapital + flows.Sum(f => f.SignedAmount);
        }

        // Build cash flow timeline
        var startDate = portfolio.CreatedAt.Date;
        var endDate = DateTime.UtcNow.Date;
        var totalDays = (endDate - startDate).TotalDays;

        if (totalDays <= 0) return 0;

        // Newton-Raphson to find IRR
        decimal rate = 0.1m; // Initial guess: 10%
        const int maxIterations = 100;
        const decimal tolerance = 0.0001m;

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
                break;
            }
            rate = newRate;
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

        decimal currentValue;
        try
        {
            var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
            var netFlow = flows.Sum(f => f.SignedAmount);
            var cashBalance = portfolio.InitialCapital + netFlow - pnl.TotalInvested;
            currentValue = pnl.TotalPortfolioValue + cashBalance;
        }
        catch
        {
            currentValue = portfolio.InitialCapital + flows.Sum(f => f.SignedAmount);
        }

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
}
