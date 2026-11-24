using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;

public class GetTechnicalSeriesQuery : IRequest<TechnicalSeriesResult>
{
    public string Symbol { get; set; } = null!;
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
        return await _technicalService.GetSeriesAsync(request.Symbol.ToUpper().Trim(), cancellationToken: ct);
    }
}
