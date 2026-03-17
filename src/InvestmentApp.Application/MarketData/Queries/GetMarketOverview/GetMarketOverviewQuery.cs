using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetMarketOverview;

public class GetMarketOverviewQuery : IRequest<List<MarketOverviewDto>>
{
}

public class MarketOverviewDto
{
    public string Symbol { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalValue { get; set; }
    public int? TradingStatus { get; set; }
    public decimal ForeignBuyValue { get; set; }
    public decimal ForeignSellValue { get; set; }
}

public class GetMarketOverviewQueryHandler : IRequestHandler<GetMarketOverviewQuery, List<MarketOverviewDto>>
{
    private readonly IStockInfoProvider _stockInfoProvider;

    public GetMarketOverviewQueryHandler(IStockInfoProvider stockInfoProvider)
    {
        _stockInfoProvider = stockInfoProvider;
    }

    public async Task<List<MarketOverviewDto>> Handle(GetMarketOverviewQuery request, CancellationToken cancellationToken)
    {
        var indices = await _stockInfoProvider.GetMarketOverviewAsync(cancellationToken);

        return indices.Select(idx => new MarketOverviewDto
        {
            Symbol = idx.Symbol,
            Price = idx.Price,
            Change = idx.Change,
            ChangePercent = idx.ChangePercent,
            TotalVolume = idx.TotalVolume,
            TotalValue = idx.TotalValue,
            TradingStatus = idx.TradingStatus,
            ForeignBuyValue = idx.ForeignBuyValue,
            ForeignSellValue = idx.ForeignSellValue
        }).ToList();
    }
}
