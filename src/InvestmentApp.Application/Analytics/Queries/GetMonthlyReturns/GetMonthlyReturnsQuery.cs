using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Analytics.Queries.GetMonthlyReturns;

public class GetMonthlyReturnsQuery : IRequest<MonthlyReturnsData>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetMonthlyReturnsQueryHandler : IRequestHandler<GetMonthlyReturnsQuery, MonthlyReturnsData>
{
    private readonly IPerformanceMetricsService _performanceMetricsService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetMonthlyReturnsQueryHandler(
        IPerformanceMetricsService performanceMetricsService,
        IPortfolioRepository portfolioRepository)
    {
        _performanceMetricsService = performanceMetricsService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<MonthlyReturnsData> Handle(GetMonthlyReturnsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _performanceMetricsService.GetMonthlyReturnsAsync(request.PortfolioId, cancellationToken);
    }
}
