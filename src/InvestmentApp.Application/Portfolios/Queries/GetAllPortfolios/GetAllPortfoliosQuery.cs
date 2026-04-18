using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;

public class GetAllPortfoliosQuery : IRequest<List<PortfolioSummaryDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetAllPortfoliosQueryHandler : IRequestHandler<GetAllPortfoliosQuery, List<PortfolioSummaryDto>>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICapitalFlowRepository _capitalFlowRepository;

    public GetAllPortfoliosQueryHandler(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        ICapitalFlowRepository capitalFlowRepository)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _capitalFlowRepository = capitalFlowRepository;
    }

    public async Task<List<PortfolioSummaryDto>> Handle(GetAllPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var result = new List<PortfolioSummaryDto>();

        foreach (var portfolio in portfolios)
        {
            var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
            var tradeList = trades.ToList();

            // Calculate basic portfolio metrics
            var totalInvested = tradeList
                .Where(t => t.TradeType == Domain.Entities.TradeType.BUY)
                .Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);

            var totalSold = tradeList
                .Where(t => t.TradeType == Domain.Entities.TradeType.SELL)
                .Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);

            var uniqueSymbols = tradeList.Select(t => t.Symbol).Distinct().Count();

            var netCashFlow = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolio.Id, cancellationToken);

            result.Add(new PortfolioSummaryDto
            {
                Id = portfolio.Id,
                Name = portfolio.Name,
                InitialCapital = portfolio.InitialCapital,
                NetCashFlow = netCashFlow,
                CurrentCapital = portfolio.InitialCapital + netCashFlow,
                CreatedAt = portfolio.CreatedAt,
                TradeCount = tradeList.Count,
                UniqueSymbols = uniqueSymbols,
                TotalInvested = totalInvested,
                TotalSold = totalSold
            });
        }

        return result;
    }
}

public class PortfolioSummaryDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal CurrentCapital { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TradeCount { get; set; }
    public int UniqueSymbols { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalSold { get; set; }
}
