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
    public decimal Close { get; set; }
    public decimal PriorClose { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Average { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public decimal Value { get; set; }

    // Advance / Decline
    public int Advance { get; set; }
    public int Decline { get; set; }
    public int NoChange { get; set; }
    public int Ceiling { get; set; }
    public int Floor { get; set; }

    // Foreign trading (tỷ VND)
    public decimal ForeignBuyValue { get; set; }
    public decimal ForeignSellValue { get; set; }
    public decimal ForeignWeekBuyValue { get; set; }
    public decimal ForeignWeekSellValue { get; set; }
    public decimal ForeignMonthBuyValue { get; set; }
    public decimal ForeignMonthSellValue { get; set; }
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
            Close = indexData.Close,
            PriorClose = indexData.PriorClose,
            High = indexData.High,
            Low = indexData.Low,
            Average = indexData.Average,
            Change = indexData.Change,
            ChangePercent = indexData.ChangePercent,
            Volume = indexData.Volume,
            Value = indexData.Value,
            Advance = indexData.Advance,
            Decline = indexData.Decline,
            NoChange = indexData.NoChange,
            Ceiling = indexData.Ceiling,
            Floor = indexData.Floor,
            ForeignBuyValue = indexData.ForeignBuyValue,
            ForeignSellValue = indexData.ForeignSellValue,
            ForeignWeekBuyValue = indexData.ForeignWeekBuyValue,
            ForeignWeekSellValue = indexData.ForeignWeekSellValue,
            ForeignMonthBuyValue = indexData.ForeignMonthBuyValue,
            ForeignMonthSellValue = indexData.ForeignMonthSellValue
        };
    }
}
