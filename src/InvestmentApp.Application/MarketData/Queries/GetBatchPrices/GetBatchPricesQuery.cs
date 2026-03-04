using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetBatchPrices;

public class GetBatchPricesQuery : IRequest<List<BatchPriceDto>>
{
    public List<string> Symbols { get; set; } = new();
}

public class BatchPriceDto
{
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class GetBatchPricesQueryHandler : IRequestHandler<GetBatchPricesQuery, List<BatchPriceDto>>
{
    private readonly IMarketDataProvider _marketDataProvider;

    public GetBatchPricesQueryHandler(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider;
    }

    public async Task<List<BatchPriceDto>> Handle(GetBatchPricesQuery request, CancellationToken cancellationToken)
    {
        var prices = await _marketDataProvider.GetBatchPricesAsync(request.Symbols, cancellationToken);
        return prices.Select(p => new BatchPriceDto
        {
            Symbol = p.Key,
            Date = p.Value.Date,
            Close = p.Value.Close,
            Volume = p.Value.Volume
        }).ToList();
    }
}
