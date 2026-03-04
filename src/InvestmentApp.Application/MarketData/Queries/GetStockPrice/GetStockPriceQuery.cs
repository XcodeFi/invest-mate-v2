using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetStockPrice;

public class GetStockPriceQuery : IRequest<StockPriceDto>
{
    public string Symbol { get; set; } = null!;
}

public class StockPriceDto
{
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class GetStockPriceQueryHandler : IRequestHandler<GetStockPriceQuery, StockPriceDto>
{
    private readonly IMarketDataProvider _marketDataProvider;

    public GetStockPriceQueryHandler(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider;
    }

    public async Task<StockPriceDto> Handle(GetStockPriceQuery request, CancellationToken cancellationToken)
    {
        var price = await _marketDataProvider.GetCurrentPriceAsync(request.Symbol, cancellationToken);
        if (price == null)
            throw new ArgumentException($"No price data found for symbol {request.Symbol}");

        return new StockPriceDto
        {
            Symbol = price.Symbol,
            Date = price.Date,
            Open = price.Open,
            High = price.High,
            Low = price.Low,
            Close = price.Close,
            Volume = price.Volume
        };
    }
}
