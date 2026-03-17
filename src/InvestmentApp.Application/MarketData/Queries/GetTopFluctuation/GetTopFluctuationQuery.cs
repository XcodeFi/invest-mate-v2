using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetTopFluctuation;

public class GetTopFluctuationQuery : IRequest<List<TopFluctuationDto>>
{
    /// <summary>
    /// Floor code: "10" = HOSE, "02" = HNX, "03" = UPCOM
    /// </summary>
    public string Floor { get; set; } = "10";
}

public class TopFluctuationDto
{
    public string Symbol { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ShortName { get; set; }
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal Volume { get; set; }
    public decimal CeilingPrice { get; set; }
    public decimal FloorPrice { get; set; }
    public decimal ReferencePrice { get; set; }
}

public class GetTopFluctuationQueryHandler : IRequestHandler<GetTopFluctuationQuery, List<TopFluctuationDto>>
{
    private readonly IStockInfoProvider _stockInfoProvider;

    public GetTopFluctuationQueryHandler(IStockInfoProvider stockInfoProvider)
    {
        _stockInfoProvider = stockInfoProvider;
    }

    public async Task<List<TopFluctuationDto>> Handle(GetTopFluctuationQuery request, CancellationToken cancellationToken)
    {
        var items = await _stockInfoProvider.GetTopFluctuationAsync(request.Floor, cancellationToken);

        return items.Select(item => new TopFluctuationDto
        {
            Symbol = item.Symbol,
            CompanyName = item.CompanyName,
            ShortName = item.ShortName,
            Price = item.Price,
            Change = item.Change,
            ChangePercent = item.ChangePercent,
            Volume = item.Volume,
            CeilingPrice = item.CeilingPrice,
            FloorPrice = item.FloorPrice,
            ReferencePrice = item.ReferencePrice
        }).ToList();
    }
}
