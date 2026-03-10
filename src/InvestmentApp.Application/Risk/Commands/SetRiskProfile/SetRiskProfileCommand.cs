using System.Text.Json.Serialization;
using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Risk.Commands.SetRiskProfile;

public class SetRiskProfileCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    [JsonIgnore]
    public string PortfolioId { get; set; } = null!;
    public decimal? MaxPositionSizePercent { get; set; }
    public decimal? MaxSectorExposurePercent { get; set; }
    public decimal? MaxDrawdownAlertPercent { get; set; }
    public decimal? DefaultRiskRewardRatio { get; set; }
    public decimal? MaxPortfolioRiskPercent { get; set; }
}

public class SetRiskProfileCommandHandler : IRequestHandler<SetRiskProfileCommand, string>
{
    private readonly IRiskProfileRepository _riskProfileRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public SetRiskProfileCommandHandler(
        IRiskProfileRepository riskProfileRepository,
        IPortfolioRepository portfolioRepository)
    {
        _riskProfileRepository = riskProfileRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<string> Handle(SetRiskProfileCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var existing = await _riskProfileRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);

        if (existing != null)
        {
            existing.Update(
                request.MaxPositionSizePercent,
                request.MaxSectorExposurePercent,
                request.MaxDrawdownAlertPercent,
                request.DefaultRiskRewardRatio,
                request.MaxPortfolioRiskPercent);
            await _riskProfileRepository.UpsertAsync(existing, cancellationToken);
            return existing.Id;
        }
        else
        {
            var riskProfile = new RiskProfile(
                request.PortfolioId,
                request.UserId!,
                request.MaxPositionSizePercent ?? 20m,
                request.MaxSectorExposurePercent ?? 40m,
                request.MaxDrawdownAlertPercent ?? 10m,
                request.DefaultRiskRewardRatio ?? 2.0m,
                request.MaxPortfolioRiskPercent ?? 5m);
            await _riskProfileRepository.UpsertAsync(riskProfile, cancellationToken);
            return riskProfile.Id;
        }
    }
}
