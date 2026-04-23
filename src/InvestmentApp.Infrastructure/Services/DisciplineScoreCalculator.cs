using InvestmentApp.Application.Discipline.Queries;
using InvestmentApp.Application.Discipline.Services;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Discipline Score calculator — Hybrid formula từ §D6 plan Vin-discipline:
/// SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%.
/// + 1 primitive: Stop-Honor Rate.
/// Chạm SL pre-committed KHÔNG bị penalize (Thaler-Shefrin self-control model).
/// Nới SL khi underwater → penalty.
/// </summary>
public class DisciplineScoreCalculator : IDisciplineScoreCalculator
{
    private readonly ITradePlanRepository _tradePlanRepo;
    private readonly ITradeRepository _tradeRepo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public DisciplineScoreCalculator(
        ITradePlanRepository tradePlanRepo,
        ITradeRepository tradeRepo,
        IMemoryCache cache)
    {
        _tradePlanRepo = tradePlanRepo;
        _tradeRepo = tradeRepo;
        _cache = cache;
    }

    public async Task<DisciplineScoreDto> ComputeAsync(string userId, int days, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"discipline-score:{userId}:{days}";
        if (_cache.TryGetValue(cacheKey, out DisciplineScoreDto? cached) && cached != null)
            return cached;

        var from = DateTime.UtcNow.AddDays(-days);
        var allPlans = (await _tradePlanRepo.GetByUserIdAsync(userId, cancellationToken)).ToList();
        var plansInPeriod = allPlans.Where(p => !p.IsDeleted && p.CreatedAt >= from).ToList();

        // Tập trades cho SL-Integrity + Stop-Honor Rate: chỉ plan đã Executed/Reviewed/Cancelled với TradeId(s).
        var closedPlans = plansInPeriod
            .Where(p => p.Status == TradePlanStatus.Executed
                     || p.Status == TradePlanStatus.Reviewed
                     || p.Status == TradePlanStatus.Cancelled)
            .ToList();

        // Fetch trades theo plan (N+1 nhưng chấp nhận cho V1 với solo-user).
        var tradesByPlan = new Dictionary<string, List<Trade>>();
        foreach (var plan in closedPlans)
        {
            var trades = await _tradeRepo.GetByTradePlanIdAsync(plan.Id, cancellationToken);
            tradesByPlan[plan.Id] = trades.ToList();
        }

        // Sub-metrics
        var (slIntegrity, stopHonorRate) = ComputeSlIntegrityAndStopHonor(closedPlans, tradesByPlan);
        var planQuality = ComputePlanQuality(plansInPeriod);
        var reviewTimeliness = ComputeReviewTimeliness(plansInPeriod);

        var overall = ComputeOverall(slIntegrity, planQuality, reviewTimeliness);
        var label = OverallToLabel(overall);

        var closedLossTradeCount = stopHonorRate.Total;

        var dto = new DisciplineScoreDto
        {
            Overall = overall,
            Label = label,
            Components = new DisciplineComponents
            {
                SlIntegrity = slIntegrity,
                PlanQuality = planQuality,
                ReviewTimeliness = reviewTimeliness
            },
            Primitives = new DisciplinePrimitives
            {
                StopHonorRate = stopHonorRate
            },
            SampleSize = new DisciplineSampleSize
            {
                TotalPlans = plansInPeriod.Count,
                ClosedLossTrades = closedLossTradeCount,
                DaysObserved = days
            },
            GeneratedAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, dto, CacheTtl);
        return dto;
    }

    /// <summary>
    /// SL-Integrity = max(0, StopHonorRate − SlWidenedRate) × 100.
    /// Tính luôn StopHonorRate primitive vì cùng data.
    /// </summary>
    internal static (int? SlIntegrity, StopHonorRateDto StopHonor) ComputeSlIntegrityAndStopHonor(
        List<TradePlan> closedPlans,
        IReadOnlyDictionary<string, List<Trade>> tradesByPlan)
    {
        int lossHit = 0, lossTotal = 0;
        int widenedCount = 0, widenableCount = 0;

        foreach (var plan in closedPlans)
        {
            var trades = tradesByPlan.GetValueOrDefault(plan.Id) ?? new List<Trade>();
            if (trades.Count == 0) continue;

            var isBuy = plan.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);

            // Entries vs Exits. Buy plan: entries=BUY trades, exits=SELL. Sell plan: flip.
            var entries = trades.Where(t => isBuy ? t.TradeType == TradeType.BUY : t.TradeType == TradeType.SELL).ToList();
            var exits = trades.Where(t => isBuy ? t.TradeType == TradeType.SELL : t.TradeType == TradeType.BUY).ToList();
            if (entries.Count == 0 || exits.Count == 0) continue;

            // Weighted avg prices.
            var entryQty = entries.Sum(t => t.Quantity);
            var exitQty = exits.Sum(t => t.Quantity);
            if (entryQty == 0 || exitQty == 0) continue;

            var avgEntry = entries.Sum(t => t.Price * t.Quantity) / entryQty;
            var avgExit = exits.Sum(t => t.Price * t.Quantity) / exitQty;

            // Loss check: Buy => avgExit < avgEntry; Sell => avgExit > avgEntry.
            bool isLoss = isBuy ? avgExit < avgEntry : avgExit > avgEntry;
            if (!isLoss) continue;

            lossTotal++;

            // Stop honored: Buy => avgExit ≥ plannedSL; Sell => avgExit ≤ plannedSL.
            bool honored = isBuy ? avgExit >= plan.StopLoss : avgExit <= plan.StopLoss;
            if (honored) lossHit++;
        }

        // SL widened underwater detection across ALL closed plans (not just loss).
        foreach (var plan in closedPlans)
        {
            var history = plan.StopLossHistory;
            if (history == null || history.Count == 0) continue;

            widenableCount++;
            var isBuy = plan.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);

            bool anyWidenedUnderwater = history.Any(entry =>
                // Widened: Buy => NewPrice < OldPrice (SL xa entry hơn). Sell => NewPrice > OldPrice.
                isBuy ? entry.NewPrice < entry.OldPrice : entry.NewPrice > entry.OldPrice);

            if (anyWidenedUnderwater) widenedCount++;
        }

        if (lossTotal == 0)
        {
            return (null, new StopHonorRateDto { Value = -1m, Hit = 0, Total = 0 });
        }

        var stopHonorRatio = (decimal)lossHit / lossTotal;
        var widenedRatio = widenableCount > 0 ? (decimal)widenedCount / widenableCount : 0m;
        var slIntegrityRaw = Math.Max(0m, stopHonorRatio - widenedRatio) * 100m;

        var stopHonorDto = new StopHonorRateDto
        {
            Value = Math.Round(stopHonorRatio, 4),
            Hit = lossHit,
            Total = lossTotal
        };

        return ((int)Math.Round(slIntegrityRaw), stopHonorDto);
    }

    /// <summary>
    /// Plan Quality = % plan không-Legacy trong period pass gate size-based.
    /// Chỉ tính các plan Ready/InProgress/Executed/Reviewed (plan đã rời Draft).
    /// </summary>
    internal static int? ComputePlanQuality(List<TradePlan> plansInPeriod)
    {
        var nonLegacyPlansPastDraft = plansInPeriod.Where(p =>
            !p.LegacyExempt
            && p.Status != TradePlanStatus.Draft
            && p.Status != TradePlanStatus.Cancelled).ToList();

        if (nonLegacyPlansPastDraft.Count == 0) return null;

        int pass = nonLegacyPlansPastDraft.Count(p => PassesGate(p));
        return (int)Math.Round(pass * 100.0 / nonLegacyPlansPastDraft.Count);
    }

    private static bool PassesGate(TradePlan plan)
    {
        decimal size = plan.Quantity * plan.EntryPrice;
        bool strict = plan.AccountBalance.HasValue
                      && plan.AccountBalance.Value > 0m
                      && size >= plan.AccountBalance.Value * 0.05m;

        if (strict)
        {
            if (string.IsNullOrWhiteSpace(plan.Thesis) || plan.Thesis.Length < 30) return false;
            if (plan.InvalidationCriteria == null || plan.InvalidationCriteria.Count == 0) return false;
            if (plan.InvalidationCriteria.Any(r => string.IsNullOrWhiteSpace(r.Detail) || r.Detail.Length < 20)) return false;
            return true;
        }
        else
        {
            return !string.IsNullOrWhiteSpace(plan.Thesis) && plan.Thesis.Length >= 15;
        }
    }

    /// <summary>
    /// Review Timeliness = % plan Ready/InProgress với CheckDate/ExpectedReviewDate đã qua ≥ 3 ngày
    /// mà IsTriggered vẫn false (chưa review). Inverse: % đã review đúng hạn.
    /// </summary>
    internal static int? ComputeReviewTimeliness(List<TradePlan> plansInPeriod)
    {
        var now = DateTime.UtcNow;

        var activePlansWithDue = plansInPeriod.Where(p =>
            (p.Status == TradePlanStatus.Ready || p.Status == TradePlanStatus.InProgress)
            && HasDueReview(p, now)).ToList();

        if (activePlansWithDue.Count == 0) return null;

        int onTime = activePlansWithDue.Count(p => IsReviewedOnTime(p, now));
        return (int)Math.Round(onTime * 100.0 / activePlansWithDue.Count);
    }

    private static bool HasDueReview(TradePlan plan, DateTime now)
    {
        if (plan.ExpectedReviewDate.HasValue && plan.ExpectedReviewDate.Value <= now) return true;
        if (plan.InvalidationCriteria != null
            && plan.InvalidationCriteria.Any(r => r.CheckDate.HasValue && r.CheckDate.Value <= now))
            return true;
        return false;
    }

    private static bool IsReviewedOnTime(TradePlan plan, DateTime now)
    {
        // Reviewed on time = any invalidation rule has TriggeredAt within 3 days of its CheckDate,
        // OR ExpectedReviewDate is in the future (still within grace window).
        if (plan.ExpectedReviewDate.HasValue && plan.ExpectedReviewDate.Value > now.AddDays(-3)) return true;

        if (plan.InvalidationCriteria == null) return false;
        return plan.InvalidationCriteria.Any(r =>
            r.IsTriggered && r.TriggeredAt.HasValue
            && r.CheckDate.HasValue
            && r.TriggeredAt.Value <= r.CheckDate.Value.AddDays(3));
    }

    /// <summary>
    /// Weighted composite: SL 50% + PQ 30% + RT 20%.
    /// Null sub-metrics → re-normalize weights trên non-null (M11 fix).
    /// Public để test trực tiếp logic weighted avg.
    /// </summary>
    public static int? ComputeOverall(int? slIntegrity, int? planQuality, int? reviewTimeliness)
    {
        var pairs = new List<(int Value, double Weight)>();
        if (slIntegrity.HasValue) pairs.Add((slIntegrity.Value, 0.50));
        if (planQuality.HasValue) pairs.Add((planQuality.Value, 0.30));
        if (reviewTimeliness.HasValue) pairs.Add((reviewTimeliness.Value, 0.20));

        if (pairs.Count == 0) return null;

        double totalWeight = pairs.Sum(p => p.Weight);
        double weightedSum = pairs.Sum(p => p.Value * p.Weight);
        return (int)Math.Round(weightedSum / totalWeight);
    }

    private static string OverallToLabel(int? overall) => overall switch
    {
        null => "Chưa đủ dữ liệu",
        >= 80 => "Kỷ luật Vin",
        >= 60 => "Cần cải thiện",
        _ => "Trôi dạt"
    };
}
