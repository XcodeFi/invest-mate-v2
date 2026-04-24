using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Simulates "if these cash flows had gone to a savings account at annual rate r,
/// compounded monthly, what would the ending balance be at <paramref name="asOf"/>?"
///
/// **Pure math** — no repository dependency. Caller fetches + filters flows, passes them in.
/// Used for the **opportunity cost** comparison feature — portfolio performance vs. hypothetical savings.
///
/// Semantics:
/// - **Running balance iterative** — each flow updates a single running balance that compounds forward.
///   Does NOT compound each flow independently (that would zero out interest earned before a withdrawal).
/// - Caller is responsible for filtering (Deposit/Withdraw only — excludes Dividend/Interest/Fee to avoid
///   double-counting investment returns).
/// - **Monthly compound** — `(1 + r/12)^months` between consecutive flow dates. Closer to VN banking
///   reality than daily compound.
/// </summary>
public interface IHypotheticalSavingsReturnService
{
    /// <summary>
    /// Compute hypothetical ending balance at <paramref name="asOf"/> given the (already-filtered, already-sorted by date) flows.
    /// </summary>
    decimal CalculateEndValue(IReadOnlyList<CapitalFlow> flows, decimal annualRate, DateTime asOf);
}
