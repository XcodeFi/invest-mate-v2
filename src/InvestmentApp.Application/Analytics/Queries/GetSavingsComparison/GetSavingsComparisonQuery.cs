using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Analytics.Queries.GetSavingsComparison;

/// <summary>
/// Query to produce SavingsComparisonDto — actual portfolio value + hypothetical savings value + alpha.
///
/// Design notes (from plan):
/// - <see cref="AnnualRate"/> null → handler resolves: weighted-avg of user's Savings accounts → fallback 5% if no rate data.
/// - <see cref="AsOf"/> null → DateTime.UtcNow.
/// - Handler returns flows + actual curve so FE can recompute hypothetical curve client-side when user changes rate.
/// </summary>
public class GetSavingsComparisonQuery : IRequest<SavingsComparisonDto>
{
    public string UserId { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public decimal? AnnualRate { get; set; }
    public DateTime? AsOf { get; set; }
}

public class SavingsComparisonDto
{
    public decimal ActualValue { get; set; }
    public decimal HypotheticalValue { get; set; }
    public decimal OpportunityCost { get; set; }
    /// <summary>Null khi không tính được ổn định (hypothetical ≤ 0 do withdraw-heavy portfolio).</summary>
    public decimal? OpportunityCostPercent { get; set; }
    public decimal UsedRate { get; set; }
    public string RateSource { get; set; } = null!;  // "user-savings-avg" / "fallback-5" / "manual"
    public int SavingsAccountsCounted { get; set; }
    public int SavingsAccountsTotal { get; set; }

    public List<CurvePoint> ActualCurve { get; set; } = new();
    public List<FlowEvent> Flows { get; set; } = new();   // for client-side hypothetical recompute

    public decimal? CagrActual { get; set; }
    public decimal? AlphaAnnualized { get; set; }  // null when days < 365
    public decimal? PeriodReturnDiff { get; set; } // used when days < 365

    public DateTime AsOf { get; set; }
    public DateTime? FirstFlowDate { get; set; }
}

public class CurvePoint
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

public class FlowEvent
{
    public DateTime Date { get; set; }
    public decimal SignedAmount { get; set; }
}

public class GetSavingsComparisonQueryHandler : IRequestHandler<GetSavingsComparisonQuery, SavingsComparisonDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IFinancialProfileRepository _profileRepository;
    private readonly ICapitalFlowRepository _flowRepository;
    private readonly IPnLService _pnlService;
    private readonly IPerformanceMetricsService _performanceService;
    private readonly IHypotheticalSavingsReturnService _hypotheticalService;

    /// <summary>Fallback rate khi user không có Savings account hoặc không nhập InterestRate. VN bank 12T ~4.7-5% (2026).</summary>
    private const decimal DefaultFallbackRate = 0.05m;
    private const decimal MinRate = -0.1m;
    private const decimal MaxRate = 0.5m;

    public GetSavingsComparisonQueryHandler(
        IPortfolioRepository portfolioRepository,
        IFinancialProfileRepository profileRepository,
        ICapitalFlowRepository flowRepository,
        IPnLService pnlService,
        IPerformanceMetricsService performanceService,
        IHypotheticalSavingsReturnService hypotheticalService)
    {
        _portfolioRepository = portfolioRepository;
        _profileRepository = profileRepository;
        _flowRepository = flowRepository;
        _pnlService = pnlService;
        _performanceService = performanceService;
        _hypotheticalService = hypotheticalService;
    }

    public async Task<SavingsComparisonDto> Handle(GetSavingsComparisonQuery request, CancellationToken ct)
    {
        // Ownership check (match convention with sibling analytics queries — single ArgumentException)
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, ct);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        // Normalize asOf to date (no time-of-day) to match CapitalFlow.FlowDate granularity and avoid
        // a partial-day of compound interest leaking into the result when asOf = DateTime.UtcNow.
        var asOf = (request.AsOf ?? DateTime.UtcNow).Date;

        // Rate resolution (D2 + D3)
        var (usedRate, source, counted, total) = await ResolveRateAsync(request, ct);
        if (usedRate < MinRate || usedRate > MaxRate)
            throw new InvalidOperationException($"Rate {usedRate:P2} out of sanity range ({MinRate:P0} to {MaxRate:P0})");

        // Actual portfolio value
        var pnl = await _pnlService.CalculatePortfolioPnLAsync(request.PortfolioId, ct);
        var actualValue = pnl.TotalMarketValue;

        // Single flow fetch — filter to Deposit/Withdraw once, pass to pure-math service.
        var allFlows = await _flowRepository.GetByPortfolioIdAsync(request.PortfolioId, ct);
        var relevantFlows = allFlows
            .Where(f => (f.Type == CapitalFlowType.Deposit || f.Type == CapitalFlowType.Withdraw) && f.FlowDate <= asOf)
            .OrderBy(f => f.FlowDate)
            .ToList();

        var hypotheticalValue = _hypotheticalService.CalculateEndValue(relevantFlows, usedRate, asOf);

        var flowEvents = relevantFlows
            .Select(f => new FlowEvent { Date = f.FlowDate, SignedAmount = f.SignedAmount })
            .ToList();
        var firstFlowDate = flowEvents.FirstOrDefault()?.Date;

        // Actual equity curve (reuse existing service)
        var equityCurve = await _performanceService.GetEquityCurveAsync(request.PortfolioId, ct);
        var actualCurve = equityCurve.Points
            .Select(p => new CurvePoint { Date = p.Date, Value = p.PortfolioValue })
            .ToList();

        // Metrics
        var opportunityCost = actualValue - hypotheticalValue;
        decimal? opportunityCostPercent = hypotheticalValue > 0m
            ? opportunityCost / hypotheticalValue * 100m
            : (decimal?)null;  // withdraw-heavy portfolio → hypothetical ≤ 0, percent is undefined

        decimal? cagrActual = null;
        decimal? alphaAnnualized = null;
        decimal? periodReturnDiff = null;

        if (firstFlowDate.HasValue)
        {
            var days = (asOf - firstFlowDate.Value).TotalDays;
            var totalDeposits = flowEvents.Sum(f => f.SignedAmount);

            if (days >= 365 && totalDeposits > 0m)
            {
                // CAGR_actual = (actualValue / totalDeposits)^(365/days) - 1. Only meaningful for positive principal.
                var ratio = (double)(actualValue / totalDeposits);
                if (ratio > 0)
                {
                    cagrActual = (decimal)(Math.Pow(ratio, 365.0 / days) - 1.0);
                    alphaAnnualized = cagrActual - usedRate;
                }
            }
            else if (totalDeposits > 0m)
            {
                // Period return diff (actual - savings) on contributed principal basis
                var actualReturn = (actualValue - totalDeposits) / totalDeposits;
                var savingsReturn = (hypotheticalValue - totalDeposits) / totalDeposits;
                periodReturnDiff = actualReturn - savingsReturn;
            }
        }

        return new SavingsComparisonDto
        {
            ActualValue = actualValue,
            HypotheticalValue = hypotheticalValue,
            OpportunityCost = opportunityCost,
            OpportunityCostPercent = opportunityCostPercent,
            UsedRate = usedRate,
            RateSource = source,
            SavingsAccountsCounted = counted,
            SavingsAccountsTotal = total,
            ActualCurve = actualCurve,
            Flows = flowEvents,
            CagrActual = cagrActual,
            AlphaAnnualized = alphaAnnualized,
            PeriodReturnDiff = periodReturnDiff,
            AsOf = asOf,
            FirstFlowDate = firstFlowDate,
        };
    }

    private async Task<(decimal rate, string source, int counted, int total)> ResolveRateAsync(
        GetSavingsComparisonQuery request, CancellationToken ct)
    {
        if (request.AnnualRate.HasValue)
            return (request.AnnualRate.Value, "manual", 0, 0);

        var profile = await _profileRepository.GetByUserIdAsync(request.UserId, ct);
        var savings = profile?.Accounts
            .Where(a => a.Type == FinancialAccountType.Savings)
            .ToList() ?? new List<FinancialAccount>();
        var total = savings.Count;
        // Weighted avg: only accounts with InterestRate set + positive balance (0-balance contributes 0 weight).
        var withRate = savings.Where(a => a.InterestRate.HasValue && a.Balance > 0m).ToList();
        var counted = withRate.Count;
        var totalBalance = withRate.Sum(a => a.Balance);

        if (totalBalance > 0m)
        {
            // InterestRate is stored as percent (e.g. 5.5 for 5.5%). Convert to decimal rate.
            var weighted = withRate.Sum(a => a.Balance * a.InterestRate!.Value / 100m) / totalBalance;
            return (weighted, "user-savings-avg", counted, total);
        }

        return (DefaultFallbackRate, "fallback-5", counted, total);
    }
}
