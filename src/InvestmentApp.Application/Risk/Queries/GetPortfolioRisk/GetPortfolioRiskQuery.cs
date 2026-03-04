using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;

public class GetPortfolioRiskQuery : IRequest<PortfolioRiskSummary>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetPortfolioRiskQueryHandler : IRequestHandler<GetPortfolioRiskQuery, PortfolioRiskSummary>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPortfolioRiskQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<PortfolioRiskSummary> Handle(GetPortfolioRiskQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.GetPortfolioRiskSummaryAsync(request.PortfolioId, cancellationToken);
    }
}
