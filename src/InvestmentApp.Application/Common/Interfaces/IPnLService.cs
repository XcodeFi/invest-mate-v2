using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.ValueObjects;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Application.Interfaces;

public interface IPnLService
{
    Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task<PositionPnL> CalculatePositionPnLAsync(string portfolioId, StockSymbol symbol, CancellationToken cancellationToken = default);
    Task UpdatePortfolioPositionsAsync(string portfolioId, CancellationToken cancellationToken = default);
}