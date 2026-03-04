using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Analytics.Queries.GetPerformance;

public class GetPerformanceQuery : IRequest<PerformanceSummary>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetPerformanceQueryHandler : IRequestHandler<GetPerformanceQuery, PerformanceSummary>
{
    private readonly IPerformanceMetricsService _performanceMetricsService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPerformanceQueryHandler(
        IPerformanceMetricsService performanceMetricsService,
        IPortfolioRepository portfolioRepository)
    {
        _performanceMetricsService = performanceMetricsService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<PerformanceSummary> Handle(GetPerformanceQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _performanceMetricsService.GetFullPerformanceSummaryAsync(request.PortfolioId, cancellationToken);
    }
}
