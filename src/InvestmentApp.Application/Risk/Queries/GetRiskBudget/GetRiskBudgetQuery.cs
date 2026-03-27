using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetRiskBudget;

public class GetRiskBudgetQuery : IRequest<RiskBudgetStatus>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetRiskBudgetQueryHandler : IRequestHandler<GetRiskBudgetQuery, RiskBudgetStatus>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetRiskBudgetQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<RiskBudgetStatus> Handle(GetRiskBudgetQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.CheckRiskBudgetAsync(request.PortfolioId, cancellationToken);
    }
}
