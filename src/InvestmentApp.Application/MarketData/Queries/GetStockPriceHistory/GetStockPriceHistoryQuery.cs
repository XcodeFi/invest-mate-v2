using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;

public class GetStockPriceHistoryQuery : IRequest<List<StockPriceHistoryDto>>
{
    public string Symbol { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class StockPriceHistoryDto
{
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class GetStockPriceHistoryQueryHandler : IRequestHandler<GetStockPriceHistoryQuery, List<StockPriceHistoryDto>>
{
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly IMarketDataProvider _marketDataProvider;

    public GetStockPriceHistoryQueryHandler(
        IStockPriceRepository stockPriceRepository,
        IMarketDataProvider marketDataProvider)
    {
        _stockPriceRepository = stockPriceRepository;
        _marketDataProvider = marketDataProvider;
    }

    public async Task<List<StockPriceHistoryDto>> Handle(GetStockPriceHistoryQuery request, CancellationToken cancellationToken)
    {
        // First try to get from database
        var dbPrices = await _stockPriceRepository.GetBySymbolAsync(request.Symbol, request.From, request.To, cancellationToken);
        var dbList = dbPrices.ToList();

        if (dbList.Any())
        {
            return dbList.Select(p => new StockPriceHistoryDto
            {
                Symbol = p.Symbol,
                Date = p.Date,
                Open = p.Open,
                High = p.High,
                Low = p.Low,
                Close = p.Close,
                Volume = p.Volume
            }).ToList();
        }

        // Fallback to market data provider
        var prices = await _marketDataProvider.GetHistoricalPricesAsync(request.Symbol, request.From, request.To, cancellationToken);
        return prices.Select(p => new StockPriceHistoryDto
        {
            Symbol = p.Symbol,
            Date = p.Date,
            Open = p.Open,
            High = p.High,
            Low = p.Low,
            Close = p.Close,
            Volume = p.Volume
        }).ToList();
    }
}
