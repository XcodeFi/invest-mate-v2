using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;

public class GetVolatilitySizingForPlanQuery : IRequest<VolatilitySizingResult>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public int Quantity { get; set; }
}

public class GetVolatilitySizingForPlanQueryHandler
    : IRequestHandler<GetVolatilitySizingForPlanQuery, VolatilitySizingResult>
{
    private readonly IVolatilityBudgetService _volatilityBudgetService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetVolatilitySizingForPlanQueryHandler(
        IVolatilityBudgetService volatilityBudgetService,
        IPortfolioRepository portfolioRepository)
    {
        _volatilityBudgetService = volatilityBudgetService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<VolatilitySizingResult> Handle(
        GetVolatilitySizingForPlanQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _volatilityBudgetService.GetSizingForPlanAsync(
            request.PortfolioId, request.Symbol, request.EntryPrice, request.Quantity, cancellationToken);
    }
}
