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

    /// <summary>true: giá đã điều chỉnh theo sự kiện quyền (cổ tức, thưởng, chia tách); false: chưa điều chỉnh (kho đã lưu hoặc nguồn dự phòng).</summary>
    public bool? Adjusted { get; set; }
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
        // 1) Nguồn ưu tiên: nến thật ĐÃ điều chỉnh theo sự kiện quyền (provider tự dự phòng nội bộ).
        var provided = await _marketDataProvider.GetHistoricalPricesAsync(request.Symbol, request.From, request.To, cancellationToken);
        if (provided.Count > 0 && provided.All(p => p.IsAdjusted))
            return provided.Select(p => ToDto(p.Symbol, p.Date, p.Open, p.High, p.Low, p.Close, p.Volume, true)).ToList();

        // 2) Nguồn điều chỉnh không có: kho đã lưu có nến thật (O/H/L/C khác nhau) nhưng CHƯA điều chỉnh,
        //    tốt hơn chuỗi dự phòng chỉ có giá đóng cửa (O=H=L=C).
        var dbList = (await _stockPriceRepository.GetBySymbolAsync(request.Symbol, request.From, request.To, cancellationToken)).ToList();
        if (dbList.Count > 0)
            return dbList.Select(p => ToDto(p.Symbol, p.Date, p.Open, p.High, p.Low, p.Close, p.Volume, false)).ToList();

        // 3) Còn lại: chuỗi dự phòng của provider (chưa điều chỉnh), có thể rỗng.
        return provided.Select(p => ToDto(p.Symbol, p.Date, p.Open, p.High, p.Low, p.Close, p.Volume, false)).ToList();
    }

    private static StockPriceHistoryDto ToDto(string symbol, DateTime date, decimal open, decimal high, decimal low,
        decimal close, long volume, bool adjusted) => new()
    {
        Symbol = symbol, Date = date, Open = open, High = high, Low = low, Close = close, Volume = volume,
        Adjusted = adjusted
    };
}
