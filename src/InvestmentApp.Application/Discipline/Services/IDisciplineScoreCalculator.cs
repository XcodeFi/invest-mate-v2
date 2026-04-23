using InvestmentApp.Application.Discipline.Queries;

namespace InvestmentApp.Application.Discipline.Services;

/// <summary>
/// Tính Discipline Score cho user (§D6 plan Vin-discipline, Hybrid formula).
/// Weighted: SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%.
/// </summary>
public interface IDisciplineScoreCalculator
{
    Task<DisciplineScoreDto> ComputeAsync(string userId, int days, CancellationToken cancellationToken = default);
}
