using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;

/// <summary>
/// Kỳ vọng theo R của các chiến dịch ĐÃ ĐÓNG (kế hoạch đã review), nhóm theo chiến lược của kế hoạch.
/// Chỉ lệnh mua; kế hoạch không tính được R (thiếu rủi ro ban đầu) bị loại khỏi mẫu chứ không tính 0R.
/// Số liệu cũ theo lệnh (win rate/expectancy theo tiền) giữ nguyên ở nơi khác.
/// </summary>
public class GetStrategyExpectancyQuery : IRequest<StrategyExpectancyDto>
{
    public string UserId { get; set; } = null!;
}

public class StrategyExpectancyDto
{
    /// <summary>Tất cả chiến dịch đã đóng của người dùng (gồm cả nhóm không gắn chiến lược).</summary>
    public ExpectancyStatsDto Total { get; set; } = new();
    public List<ExpectancyStatsDto> Strategies { get; set; } = new();
}

public class ExpectancyStatsDto
{
    public string? StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    /// <summary>Số chiến dịch đã đóng có R (gồm cả hoà).</summary>
    public int N { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public decimal? WinRate { get; set; }
    public decimal? WinRateLow95 { get; set; }
    public decimal? WinRateHigh95 { get; set; }
    public decimal? AvgWinR { get; set; }
    public decimal? AvgLossR { get; set; }
    public decimal? ExpectancyR { get; set; }
    public decimal? ProfitFactorR { get; set; }
}

public class GetStrategyExpectancyQueryHandler : IRequestHandler<GetStrategyExpectancyQuery, StrategyExpectancyDto>
{
    public const string NoStrategyName = "Không gắn chiến lược";
    public const string DeletedStrategyName = "Chiến lược không còn tồn tại";
    public const string TotalName = "Tất cả";

    private readonly ITradePlanRepository _plans;
    private readonly IStrategyRepository _strategies;

    public GetStrategyExpectancyQueryHandler(ITradePlanRepository plans, IStrategyRepository strategies)
    {
        _plans = plans;
        _strategies = strategies;
    }

    public async Task<StrategyExpectancyDto> Handle(GetStrategyExpectancyQuery request, CancellationToken ct)
    {
        var plans = await _plans.GetByUserIdAsync(request.UserId, ct);
        var strategies = await _strategies.GetByUserIdAsync(request.UserId, ct);
        var names = strategies.ToDictionary(s => s.Id, s => s.Name);

        // R tính một lần cho mỗi kế hoạch: giá trị đã lưu, hoặc tính khi đọc cho review cũ (không ghi lại).
        var samples = plans
            .Where(p => p.UserId == request.UserId
                        && !p.IsDeleted
                        && p.Status == TradePlanStatus.Reviewed
                        && string.Equals(p.Direction, "Buy", StringComparison.OrdinalIgnoreCase))
            .Select(p => (Plan: p, R: RMath.ClosedRealizedR(p)))
            .Where(x => x.R.HasValue)
            .Select(x => (x.Plan.StrategyId, R: x.R!.Value))
            .ToList();

        var groups = samples
            .GroupBy(x => string.IsNullOrEmpty(x.StrategyId) ? null : x.StrategyId)
            .Select(g => ToDto(g.Key, NameOf(g.Key, names), g.Select(x => x.R)))
            .OrderBy(d => d.StrategyId == null ? 1 : 0)       // "Không gắn chiến lược" xuống cuối
            .ThenByDescending(d => d.N)
            .ThenBy(d => d.StrategyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new StrategyExpectancyDto
        {
            Total = ToDto(null, TotalName, samples.Select(x => x.R)),
            Strategies = groups
        };
    }

    private static string NameOf(string? strategyId, Dictionary<string, string> names)
        => strategyId == null ? NoStrategyName
            : names.TryGetValue(strategyId, out var name) ? name : DeletedStrategyName;

    private static ExpectancyStatsDto ToDto(string? strategyId, string name, IEnumerable<decimal> rs)
    {
        var s = ExpectancyCalculator.Compute(rs);
        return new ExpectancyStatsDto
        {
            StrategyId = strategyId,
            StrategyName = name,
            N = s.N, Wins = s.Wins, Losses = s.Losses, Draws = s.Draws,
            WinRate = s.WinRate, WinRateLow95 = s.WinRateLow95, WinRateHigh95 = s.WinRateHigh95,
            AvgWinR = s.AvgWinR, AvgLossR = s.AvgLossR,
            ExpectancyR = s.ExpectancyR, ProfitFactorR = s.ProfitFactorR
        };
    }
}
