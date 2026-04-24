using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Hypothetical savings calculator — see <see cref="IHypotheticalSavingsReturnService"/> for contract.
/// Pure math: running-balance iterative forward simulation with monthly compounding between flow dates.
/// </summary>
public class HypotheticalSavingsReturnService : IHypotheticalSavingsReturnService
{
    // Average days per month (365.25/12). Used for fractional month compounding between flow dates.
    // 365.25 instead of 365 to amortize leap years (~quarter day per year).
    private const double DaysPerMonth = 365.25 / 12.0;

    public decimal CalculateEndValue(IReadOnlyList<CapitalFlow> flows, decimal annualRate, DateTime asOf)
    {
        if (flows.Count == 0) return 0m;

        var monthlyRate = annualRate / 12m;
        decimal balance = 0m;
        DateTime? prevDate = null;

        // Running balance iterative. Between each consecutive flow, compound by elapsed months.
        foreach (var flow in flows)
        {
            if (prevDate.HasValue)
            {
                balance = CompoundForward(balance, monthlyRate, prevDate.Value, flow.FlowDate);
            }
            balance += flow.SignedAmount;
            prevDate = flow.FlowDate;
        }

        // Final roll from last flow to asOf
        if (prevDate.HasValue && prevDate.Value < asOf)
        {
            balance = CompoundForward(balance, monthlyRate, prevDate.Value, asOf);
        }

        return balance;
    }

    private static decimal CompoundForward(decimal balance, decimal monthlyRate, DateTime from, DateTime to)
    {
        var months = (decimal)((to - from).TotalDays / DaysPerMonth);
        if (months <= 0m) return balance;
        var factor = (decimal)Math.Pow((double)(1m + monthlyRate), (double)months);
        return balance * factor;
    }
}
