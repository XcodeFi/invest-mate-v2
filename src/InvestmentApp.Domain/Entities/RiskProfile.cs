using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Risk profile settings for a portfolio - defines risk tolerance and limits.
/// </summary>
public class RiskProfile : AggregateRoot
{
    public string PortfolioId { get; private set; }
    public string UserId { get; private set; }
    public decimal MaxPositionSizePercent { get; private set; }
    public decimal MaxSectorExposurePercent { get; private set; }
    public decimal MaxDrawdownAlertPercent { get; private set; }
    public decimal DefaultRiskRewardRatio { get; private set; }
    public decimal MaxPortfolioRiskPercent { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public RiskProfile() { } // MongoDB

    public RiskProfile(string portfolioId, string userId,
        decimal maxPositionSizePercent = 20m,
        decimal maxSectorExposurePercent = 40m,
        decimal maxDrawdownAlertPercent = 10m,
        decimal defaultRiskRewardRatio = 2.0m,
        decimal maxPortfolioRiskPercent = 5m)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        MaxPositionSizePercent = maxPositionSizePercent;
        MaxSectorExposurePercent = maxSectorExposurePercent;
        MaxDrawdownAlertPercent = maxDrawdownAlertPercent;
        DefaultRiskRewardRatio = defaultRiskRewardRatio;
        MaxPortfolioRiskPercent = maxPortfolioRiskPercent;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        decimal? maxPositionSizePercent = null,
        decimal? maxSectorExposurePercent = null,
        decimal? maxDrawdownAlertPercent = null,
        decimal? defaultRiskRewardRatio = null,
        decimal? maxPortfolioRiskPercent = null)
    {
        if (maxPositionSizePercent.HasValue) MaxPositionSizePercent = maxPositionSizePercent.Value;
        if (maxSectorExposurePercent.HasValue) MaxSectorExposurePercent = maxSectorExposurePercent.Value;
        if (maxDrawdownAlertPercent.HasValue) MaxDrawdownAlertPercent = maxDrawdownAlertPercent.Value;
        if (defaultRiskRewardRatio.HasValue) DefaultRiskRewardRatio = defaultRiskRewardRatio.Value;
        if (maxPortfolioRiskPercent.HasValue) MaxPortfolioRiskPercent = maxPortfolioRiskPercent.Value;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
