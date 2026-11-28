using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;

public class GetTechnicalSeriesQuery : IRequest<TechnicalSeriesResult>
{
    public string Symbol { get; set; } = null!;

    /// <summary>
    /// Số tháng lịch sử dùng để tính chỉ báo (mặc định 12, kẹp trong 3 đến 36). Trang xem khoảng ngắn xin dài hơn khoảng hiển thị
    /// để chỉ báo có đủ dữ liệu khởi động rồi mới cắt theo khoảng.
    /// </summary>
    public int Months { get; set; } = 12;
}

public class GetTechnicalSeriesQueryHandler : IRequestHandler<GetTechnicalSeriesQuery, TechnicalSeriesResult>
{
    private readonly ITechnicalIndicatorService _technicalService;

    public GetTechnicalSeriesQueryHandler(ITechnicalIndicatorService technicalService)
    {
        _technicalService = technicalService;
    }

    public async Task<TechnicalSeriesResult> Handle(GetTechnicalSeriesQuery request, CancellationToken ct)
    {
        return await _technicalService.GetSeriesAsync(request.Symbol.ToUpperInvariant().Trim(), Math.Clamp(request.Months, 3, 36), ct);
    }
}
