using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetStockDetail;

public class GetStockDetailQuery : IRequest<StockDetailDto>
{
    public string Symbol { get; set; } = null!;
}

public class StockDetailDto
{
    public string Symbol { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string CompanyNameEng { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string FloorCode { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal ReferencePrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CeilingPrice { get; set; }
    public decimal FloorPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal Value { get; set; }
    public decimal ForeignBuyVolume { get; set; }
    public decimal ForeignSellVolume { get; set; }
    public decimal ForeignRoom { get; set; }
    public string? LogoUrl { get; set; }
    public List<OrderBookLevelDto> Bids { get; set; } = new();
    public List<OrderBookLevelDto> Asks { get; set; } = new();
}

public class OrderBookLevelDto
{
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
}

public class GetStockDetailQueryHandler : IRequestHandler<GetStockDetailQuery, StockDetailDto>
{
    private readonly IStockInfoProvider _stockInfoProvider;

    public GetStockDetailQueryHandler(IStockInfoProvider stockInfoProvider)
    {
        _stockInfoProvider = stockInfoProvider;
    }

    public async Task<StockDetailDto> Handle(GetStockDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await _stockInfoProvider.GetStockDetailAsync(request.Symbol, cancellationToken);
        if (detail == null)
            throw new ArgumentException($"Không tìm thấy thông tin cho mã {request.Symbol}");

        return new StockDetailDto
        {
            Symbol = detail.Symbol,
            CompanyName = detail.CompanyName,
            CompanyNameEng = detail.CompanyNameEng,
            ShortName = detail.ShortName,
            Exchange = detail.Exchange,
            FloorCode = detail.FloorCode,
            Price = detail.Price,
            Change = detail.Change,
            ChangePercent = detail.ChangePercent,
            ReferencePrice = detail.ReferencePrice,
            OpenPrice = detail.OpenPrice,
            ClosePrice = detail.ClosePrice,
            HighPrice = detail.HighPrice,
            LowPrice = detail.LowPrice,
            AveragePrice = detail.AveragePrice,
            CeilingPrice = detail.CeilingPrice,
            FloorPrice = detail.FloorPrice,
            Volume = detail.Volume,
            Value = detail.Value,
            ForeignBuyVolume = detail.ForeignBuyVolume,
            ForeignSellVolume = detail.ForeignSellVolume,
            ForeignRoom = detail.ForeignRoom,
            LogoUrl = detail.LogoUrl,
            Bids = detail.Bids.Select(b => new OrderBookLevelDto { Price = b.Price, Volume = b.Volume }).ToList(),
            Asks = detail.Asks.Select(a => new OrderBookLevelDto { Price = a.Price, Volume = a.Volume }).ToList()
        };
    }
}
