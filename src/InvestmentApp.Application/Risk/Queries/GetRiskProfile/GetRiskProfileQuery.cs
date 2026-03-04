using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetRiskProfile;

public class GetRiskProfileQuery : IRequest<RiskProfileDto?>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class RiskProfileDto
{
    public string Id { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public decimal MaxPositionSizePercent { get; set; }
    public decimal MaxSectorExposurePercent { get; set; }
    public decimal MaxDrawdownAlertPercent { get; set; }
    public decimal DefaultRiskRewardRatio { get; set; }
    public decimal MaxPortfolioRiskPercent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetRiskProfileQueryHandler : IRequestHandler<GetRiskProfileQuery, RiskProfileDto?>
{
    private readonly IRiskProfileRepository _riskProfileRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetRiskProfileQueryHandler(
        IRiskProfileRepository riskProfileRepository,
        IPortfolioRepository portfolioRepository)
    {
        _riskProfileRepository = riskProfileRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<RiskProfileDto?> Handle(GetRiskProfileQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var profile = await _riskProfileRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);
        if (profile == null) return null;

        return new RiskProfileDto
        {
            Id = profile.Id,
            PortfolioId = profile.PortfolioId,
            MaxPositionSizePercent = profile.MaxPositionSizePercent,
            MaxSectorExposurePercent = profile.MaxSectorExposurePercent,
            MaxDrawdownAlertPercent = profile.MaxDrawdownAlertPercent,
            DefaultRiskRewardRatio = profile.DefaultRiskRewardRatio,
            MaxPortfolioRiskPercent = profile.MaxPortfolioRiskPercent,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }
}
