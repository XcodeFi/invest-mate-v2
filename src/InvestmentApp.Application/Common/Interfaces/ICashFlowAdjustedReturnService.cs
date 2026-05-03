namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Service for calculating cash-flow adjusted returns (TWR and MWR).
/// </summary>
public interface ICashFlowAdjustedReturnService
{
    /// <summary>
    /// Calculates Time-Weighted Return (TWR) that eliminates the effect of cash flows.
    /// </summary>
    Task<decimal> CalculateTWRAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Money-Weighted Return (MWR / IRR) that accounts for timing and size of cash flows.
    /// </summary>
    Task<decimal> CalculateMWRAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary including both TWR and MWR.
    /// </summary>
    Task<AdjustedReturnSummary> GetAdjustedReturnSummaryAsync(string portfolioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Household-level TWR + CAGR aggregated across all portfolios of a user.
    /// Builds a single aggregate snapshot series (sum of TotalValue per date,
    /// carry-forward per portfolio) and applies the same TWR formula. A
    /// portfolio entering the aggregate later is attributed as a synthetic
    /// inflow so its initial value isn't read as return.
    /// </summary>
    Task<HouseholdReturnSummary> GetHouseholdReturnSummaryAsync(string userId, CancellationToken cancellationToken = default);
}

public class AdjustedReturnSummary
{
    public string PortfolioId { get; set; } = null!;
    public decimal TimeWeightedReturn { get; set; }
    public decimal MoneyWeightedReturn { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal CurrentValue { get; set; }
    public int FlowCount { get; set; }
}

public class HouseholdReturnSummary
{
    public string UserId { get; set; } = null!;
    public int PortfolioCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TimeWeightedReturn { get; set; }
    public decimal Cagr { get; set; }
    public DateTime? FirstSnapshotDate { get; set; }
    public DateTime? LastSnapshotDate { get; set; }
    public int DaysSpanned { get; set; }
    /// <summary>True when the snapshot window is at least 1 year — i.e. CAGR is not an extreme extrapolation from a short period.</summary>
    public bool IsStable { get; set; }
}
