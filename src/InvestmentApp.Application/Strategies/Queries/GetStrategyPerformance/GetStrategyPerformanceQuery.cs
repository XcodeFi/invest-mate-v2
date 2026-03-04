using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Strategies.Queries.GetStrategyPerformance;

public class GetStrategyPerformanceQuery : IRequest<StrategyPerformanceSummary>
{
    public string StrategyId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetStrategyPerformanceQueryHandler : IRequestHandler<GetStrategyPerformanceQuery, StrategyPerformanceSummary>
{
    private readonly IStrategyPerformanceService _performanceService;

    public GetStrategyPerformanceQueryHandler(IStrategyPerformanceService performanceService)
    {
        _performanceService = performanceService;
    }

    public async Task<StrategyPerformanceSummary> Handle(GetStrategyPerformanceQuery request, CancellationToken cancellationToken)
    {
        return await _performanceService.GetPerformanceAsync(request.StrategyId, request.UserId, cancellationToken);
    }
}
