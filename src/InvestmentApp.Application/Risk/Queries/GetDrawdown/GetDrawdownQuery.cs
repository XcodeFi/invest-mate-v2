using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetDrawdown;

public class GetDrawdownQuery : IRequest<DrawdownResult>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetDrawdownQueryHandler : IRequestHandler<GetDrawdownQuery, DrawdownResult>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetDrawdownQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<DrawdownResult> Handle(GetDrawdownQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.CalculateMaxDrawdownAsync(request.PortfolioId, cancellationToken);
    }
}
