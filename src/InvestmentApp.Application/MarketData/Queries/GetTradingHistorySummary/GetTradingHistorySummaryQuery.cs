using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetTradingHistorySummary;

public class GetTradingHistorySummaryQuery : IRequest<TradingHistorySummaryDto>
{
    public string Symbol { get; set; } = null!;
}

public class TradingHistorySummaryDto
{
    public string Symbol { get; set; } = null!;
    public decimal ChangeDay { get; set; }
    public decimal ChangeWeek { get; set; }
    public decimal ChangeMonth { get; set; }
    public decimal Change3Month { get; set; }
    public decimal Change6Month { get; set; }
}

public class GetTradingHistorySummaryQueryHandler : IRequestHandler<GetTradingHistorySummaryQuery, TradingHistorySummaryDto>
{
    private readonly IStockInfoProvider _stockInfoProvider;

    public GetTradingHistorySummaryQueryHandler(IStockInfoProvider stockInfoProvider)
    {
        _stockInfoProvider = stockInfoProvider;
    }

    public async Task<TradingHistorySummaryDto> Handle(GetTradingHistorySummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _stockInfoProvider.GetTradingHistorySummaryAsync(request.Symbol, cancellationToken);
        if (summary == null)
            throw new ArgumentException($"Không tìm thấy thông tin giao dịch cho mã {request.Symbol}");

        return new TradingHistorySummaryDto
        {
            Symbol = summary.Symbol,
            ChangeDay = summary.ChangeDay,
            ChangeWeek = summary.ChangeWeek,
            ChangeMonth = summary.ChangeMonth,
            Change3Month = summary.Change3Month,
            Change6Month = summary.Change6Month
        };
    }
}
