using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;

public class GetTrailingStopAlertsQuery : IRequest<TrailingStopAlertsResult>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

/// <summary>
/// Trailing stop monitoring result with real-time alerts.
/// </summary>
public class TrailingStopAlertsResult
{
    public string PortfolioId { get; set; } = null!;
    public List<TrailingStopAlert> Alerts { get; set; } = new();
    public int TotalActiveTrailingStops { get; set; }
    public int AlertCount { get; set; }
}

/// <summary>
/// Individual trailing stop alert for a position.
/// </summary>
public class TrailingStopAlert
{
    public string Symbol { get; set; } = null!;
    public string TradeId { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal TrailingStopPercent { get; set; }
    public decimal TrailingStopPrice { get; set; }
    public decimal DistancePercent { get; set; }
    public string Severity { get; set; } = null!; // "danger" (<=2%) | "warning" (<=5%) | "safe"
    public bool ShouldUpdatePrice { get; set; }
    public decimal? NewTrailingStopPrice { get; set; }
}

public class GetTrailingStopAlertsQueryHandler : IRequestHandler<GetTrailingStopAlertsQuery, TrailingStopAlertsResult>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetTrailingStopAlertsQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<TrailingStopAlertsResult> Handle(GetTrailingStopAlertsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.GetTrailingStopAlertsAsync(request.PortfolioId, cancellationToken);
    }
}
