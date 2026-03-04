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
