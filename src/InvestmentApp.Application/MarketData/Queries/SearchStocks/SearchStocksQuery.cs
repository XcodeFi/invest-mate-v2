using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.SearchStocks;

public class SearchStocksQuery : IRequest<List<StockSearchDto>>
{
    public string Keyword { get; set; } = null!;
}

public class StockSearchDto
{
    public string Symbol { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? ShortName { get; set; }
    public string Exchange { get; set; } = null!;
    public string? LogoUrl { get; set; }
}

public class SearchStocksQueryHandler : IRequestHandler<SearchStocksQuery, List<StockSearchDto>>
{
    private readonly IStockInfoProvider _stockInfoProvider;

    public SearchStocksQueryHandler(IStockInfoProvider stockInfoProvider)
    {
        _stockInfoProvider = stockInfoProvider;
    }

    public async Task<List<StockSearchDto>> Handle(SearchStocksQuery request, CancellationToken cancellationToken)
    {
        var results = await _stockInfoProvider.SearchStocksAsync(request.Keyword, cancellationToken);

        return results.Select(r => new StockSearchDto
        {
            Symbol = r.Symbol,
            CompanyName = r.CompanyName,
            ShortName = r.ShortName,
            Exchange = r.Exchange,
            LogoUrl = r.LogoUrl
        }).ToList();
    }
}
