using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetStressTest;

public class GetStressTestQuery : IRequest<StressTestResult>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public decimal MarketChangePercent { get; set; }
}

public class GetStressTestQueryHandler : IRequestHandler<GetStressTestQuery, StressTestResult>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetStressTestQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<StressTestResult> Handle(GetStressTestQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.CalculateStressTestAsync(
            request.PortfolioId, request.MarketChangePercent, cancellationToken);
    }
}
