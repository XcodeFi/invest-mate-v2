using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;

public class GetTechnicalAnalysisQuery : IRequest<TechnicalAnalysisResult>
{
    public string Symbol { get; set; } = null!;
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
        return await _technicalService.AnalyzeAsync(request.Symbol.ToUpper().Trim(), ct);
    }
}
