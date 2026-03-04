using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Queries.GetPortfolio;

public class GetPortfolioQuery : IRequest<PortfolioDto?>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetPortfolioQueryHandler : IRequestHandler<GetPortfolioQuery, PortfolioDto?>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPortfolioQueryHandler(IPortfolioRepository portfolioRepository)
    {
        _portfolioRepository = portfolioRepository;
    }

    public async Task<PortfolioDto?> Handle(GetPortfolioQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdWithTradesAsync(request.Id, cancellationToken);

        if (portfolio == null || portfolio.UserId != request.UserId)
            return null;

        return new PortfolioDto
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            InitialCapital = portfolio.InitialCapital,
            CreatedAt = portfolio.CreatedAt,
            Trades = portfolio.Trades.Select(t => new TradeDto
            {
                Id = t.Id,
                Symbol = t.Symbol,
                TradeType = t.TradeType.ToString(),
                Quantity = t.Quantity,
                Price = t.Price,
                Fee = t.Fee,
                Tax = t.Tax,
                TradeDate = t.TradeDate
            }).ToList()
        };
    }
}

public class PortfolioDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TradeDto> Trades { get; set; } = new();
}

public class TradeDto
{
    public string Id { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public DateTime TradeDate { get; set; }
}