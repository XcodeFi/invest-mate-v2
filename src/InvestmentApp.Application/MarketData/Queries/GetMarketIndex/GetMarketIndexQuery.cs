using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetMarketIndex;

public class GetMarketIndexQuery : IRequest<MarketIndexDto>
{
    public string IndexSymbol { get; set; } = null!;
}

public class MarketIndexDto
{
    public string IndexSymbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
}

public class GetMarketIndexQueryHandler : IRequestHandler<GetMarketIndexQuery, MarketIndexDto>
{
    private readonly IMarketDataProvider _marketDataProvider;

    public GetMarketIndexQueryHandler(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider;
    }

    public async Task<MarketIndexDto> Handle(GetMarketIndexQuery request, CancellationToken cancellationToken)
    {
        var indexData = await _marketDataProvider.GetIndexDataAsync(request.IndexSymbol, cancellationToken);
        if (indexData == null)
            throw new ArgumentException($"No data found for index {request.IndexSymbol}");

        return new MarketIndexDto
        {
            IndexSymbol = indexData.IndexSymbol,
            Date = indexData.Date,
            Open = indexData.Open,
            High = indexData.High,
            Low = indexData.Low,
            Close = indexData.Close,
            Volume = indexData.Volume,
            Change = indexData.Change,
            ChangePercent = indexData.ChangePercent
        };
    }
}
