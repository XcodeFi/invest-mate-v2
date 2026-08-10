using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Risk.Queries.GetSectorExposureForPlan;

public class GetSectorExposureForPlanQuery : IRequest<SectorExposureForPlan>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal AddValue { get; set; }
}

public class GetSectorExposureForPlanQueryHandler
    : IRequestHandler<GetSectorExposureForPlanQuery, SectorExposureForPlan>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetSectorExposureForPlanQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<SectorExposureForPlan> Handle(
        GetSectorExposureForPlanQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.GetSectorExposureForPlanAsync(
            request.PortfolioId, request.Symbol, request.AddValue, cancellationToken);
    }
}
