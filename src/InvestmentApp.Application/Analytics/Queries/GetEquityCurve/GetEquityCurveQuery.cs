using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Analytics.Queries.GetEquityCurve;

public class GetEquityCurveQuery : IRequest<EquityCurveData>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetEquityCurveQueryHandler : IRequestHandler<GetEquityCurveQuery, EquityCurveData>
{
    private readonly IPerformanceMetricsService _performanceMetricsService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetEquityCurveQueryHandler(
        IPerformanceMetricsService performanceMetricsService,
        IPortfolioRepository portfolioRepository)
    {
        _performanceMetricsService = performanceMetricsService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<EquityCurveData> Handle(GetEquityCurveQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _performanceMetricsService.GetEquityCurveAsync(request.PortfolioId, cancellationToken);
    }
}
