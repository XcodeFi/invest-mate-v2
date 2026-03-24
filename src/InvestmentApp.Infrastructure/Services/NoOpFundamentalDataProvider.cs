using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// No-op implementation of IFundamentalDataProvider.
/// Used when the primary provider (TCBS) is unavailable.
/// Always returns null — callers already handle null gracefully.
/// To switch to a real provider, replace registration in Program.cs.
/// </summary>
public class NoOpFundamentalDataProvider : IFundamentalDataProvider
{
    public Task<StockFundamentalData?> GetFundamentalsAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult<StockFundamentalData?>(null);
}
