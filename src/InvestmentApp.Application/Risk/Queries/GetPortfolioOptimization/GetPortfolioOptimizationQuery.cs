using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;

public class GetPortfolioOptimizationQuery : IRequest<PortfolioOptimizationResult>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

/// <summary>
/// Portfolio optimization analysis result.
/// </summary>
public class PortfolioOptimizationResult
{
    public string PortfolioId { get; set; } = null!;
    public decimal TotalValue { get; set; }
    public decimal DiversificationScore { get; set; }
    public List<ConcentrationAlert> ConcentrationAlerts { get; set; } = new();
    public List<SectorExposure> SectorExposures { get; set; } = new();
    public List<CorrelationWarning> CorrelationWarnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Alert when a single position exceeds concentration limit.
/// </summary>
public class ConcentrationAlert
{
    public string Symbol { get; set; } = null!;
    public decimal PositionPercent { get; set; }
    public decimal Limit { get; set; }
    public string Severity { get; set; } = null!; // "warning" | "danger"
}

/// <summary>
/// Sector/industry exposure breakdown.
/// </summary>
public class SectorExposure
{
    public string Sector { get; set; } = null!;
    public List<string> Symbols { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal ExposurePercent { get; set; }
    public decimal Limit { get; set; }
    public bool IsOverweight { get; set; }
}

/// <summary>
/// Warning for highly correlated position pairs.
/// </summary>
public class CorrelationWarning
{
    public string Symbol1 { get; set; } = null!;
    public string Symbol2 { get; set; } = null!;
    public decimal Correlation { get; set; }
    public string RiskLevel { get; set; } = null!; // "high" (>0.7) | "medium" (>0.5)
}

public class GetPortfolioOptimizationQueryHandler : IRequestHandler<GetPortfolioOptimizationQuery, PortfolioOptimizationResult>
{
    private readonly IRiskCalculationService _riskCalculationService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPortfolioOptimizationQueryHandler(
        IRiskCalculationService riskCalculationService,
        IPortfolioRepository portfolioRepository)
    {
        _riskCalculationService = riskCalculationService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<PortfolioOptimizationResult> Handle(GetPortfolioOptimizationQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        return await _riskCalculationService.GetPortfolioOptimizationAsync(request.PortfolioId, cancellationToken);
    }
}
