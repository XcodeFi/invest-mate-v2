using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;

public class GetTechnicalAnalysisQuery : IRequest<TechnicalAnalysisResult>
{
    public string Symbol { get; set; } = null!;

    /// <summary>
    /// Số tháng lịch sử dùng để tính chỉ báo (mặc định 12, kẹp trong 3 đến 36). Trang xem khoảng ngắn xin dài hơn khoảng hiển thị
    /// để chỉ báo có đủ dữ liệu khởi động rồi mới cắt theo khoảng.
    /// </summary>
    public int Months { get; set; } = 12;
}

public class GetTechnicalAnalysisQueryHandler : IRequestHandler<GetTechnicalAnalysisQuery, TechnicalAnalysisResult>
{
    private readonly ITechnicalIndicatorService _technicalService;

    public GetTechnicalAnalysisQueryHandler(ITechnicalIndicatorService technicalService)
    {
        _technicalService = technicalService;
    }

    public async Task<TechnicalAnalysisResult> Handle(GetTechnicalAnalysisQuery request, CancellationToken ct)
    {
        return await _technicalService.AnalyzeAsync(request.Symbol.ToUpperInvariant().Trim(), Math.Clamp(request.Months, 3, 36), ct);
    }
}
