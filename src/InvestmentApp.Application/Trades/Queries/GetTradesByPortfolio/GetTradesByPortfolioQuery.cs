using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using MediatR;

namespace InvestmentApp.Application.Trades.Queries.GetTradesByPortfolio;

public class GetTradesByPortfolioQuery : IRequest<TradeListDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? Symbol { get; set; }
    public string? TradeType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetTradesByPortfolioQueryHandler : IRequestHandler<GetTradesByPortfolioQuery, TradeListDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;

    public GetTradesByPortfolioQueryHandler(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<TradeListDto> Handle(GetTradesByPortfolioQuery request, CancellationToken cancellationToken)
    {
        // Verify ownership
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return new TradeListDto { Items = new List<TradeDto>(), TotalCount = 0 };

        IEnumerable<Domain.Entities.Trade> trades;

        if (!string.IsNullOrEmpty(request.Symbol))
        {
            trades = await _tradeRepository.GetByPortfolioIdAndSymbolAsync(
                request.PortfolioId, request.Symbol, cancellationToken);
        }
        else
        {
            trades = await _tradeRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);
        }

        var filteredTrades = trades.AsEnumerable();

        // Filter by trade type
        if (!string.IsNullOrEmpty(request.TradeType) &&
            Enum.TryParse<Domain.Entities.TradeType>(request.TradeType, true, out var tradeType))
        {
            filteredTrades = filteredTrades.Where(t => t.TradeType == tradeType);
        }

        var orderedTrades = filteredTrades.OrderByDescending(t => t.TradeDate).ToList();
        var totalCount = orderedTrades.Count;

        var pagedTrades = orderedTrades
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TradeDto
            {
                Id = t.Id,
                PortfolioId = t.PortfolioId,
                Symbol = t.Symbol,
                TradeType = t.TradeType.ToString(),
                Quantity = t.Quantity,
                Price = t.Price,
                Fee = t.Fee,
                Tax = t.Tax,
                TradeDate = t.TradeDate,
                TotalValue = t.Quantity * t.Price + (t.TradeType == Domain.Entities.TradeType.BUY ? t.Fee + t.Tax : -(t.Fee + t.Tax)),
                TradePlanId = t.TradePlanId
            })
            .ToList();

        return new TradeListDto
        {
            Items = pagedTrades,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }
}

public class TradeDto
{
    public string Id { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public DateTime TradeDate { get; set; }
    public decimal TotalValue { get; set; }
    public string? TradePlanId { get; set; }
}

public class TradeListDto
{
    public List<TradeDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
