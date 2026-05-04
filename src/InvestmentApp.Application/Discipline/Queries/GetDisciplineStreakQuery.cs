using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Discipline.Queries;

/// <summary>
/// Streak "kỷ luật" — số ngày liên tiếp gần đây nhất user KHÔNG có SL violation.
/// Dùng cho empty state Decision Queue (`✅ Hôm nay đang kỷ luật + 🔥 X ngày`).
///
/// Định nghĩa violation: closed loss trade mà exit price không tôn trọng plan SL.
/// (Buy: avgExit lt StopLoss; Sell: avgExit gt StopLoss). Cùng logic với DisciplineScoreCalculator.
/// </summary>
public class GetDisciplineStreakQuery : IRequest<DisciplineStreakDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class DisciplineStreakDto
{
    /// <summary>Số ngày liên tiếp không có SL violation. 0 nếu chưa có data hoặc vừa có violation hôm nay.</summary>
    public int DaysWithoutViolation { get; set; }

    /// <summary>False khi user chưa có plan nào — UI ẩn streak badge nhưng vẫn hiển thị positive empty state.</summary>
    public bool HasData { get; set; }
}

public class GetDisciplineStreakQueryHandler : IRequestHandler<GetDisciplineStreakQuery, DisciplineStreakDto>
{
    private readonly ITradePlanRepository _planRepo;
    private readonly ITradeRepository _tradeRepo;

    public GetDisciplineStreakQueryHandler(ITradePlanRepository planRepo, ITradeRepository tradeRepo)
    {
        _planRepo = planRepo;
        _tradeRepo = tradeRepo;
    }

    public async Task<DisciplineStreakDto> Handle(GetDisciplineStreakQuery request, CancellationToken cancellationToken)
    {
        var allPlans = (await _planRepo.GetByUserIdAsync(request.UserId, cancellationToken))
            .Where(p => !p.IsDeleted)
            .ToList();

        if (allPlans.Count == 0)
            return new DisciplineStreakDto { DaysWithoutViolation = 0, HasData = false };

        var closedPlans = allPlans
            .Where(p => p.Status == TradePlanStatus.Executed
                     || p.Status == TradePlanStatus.Reviewed)
            .ToList();

        DateTime? latestViolationAt = null;

        foreach (var plan in closedPlans)
        {
            var trades = (await _tradeRepo.GetByTradePlanIdAsync(plan.Id, cancellationToken)).ToList();
            if (trades.Count == 0) continue;

            var violationAt = DetectViolation(plan, trades);
            if (violationAt.HasValue && (latestViolationAt == null || violationAt > latestViolationAt))
                latestViolationAt = violationAt;
        }

        var today = DateTime.UtcNow.Date;
        if (latestViolationAt.HasValue)
        {
            var days = (today - latestViolationAt.Value.Date).Days;
            return new DisciplineStreakDto { DaysWithoutViolation = Math.Max(0, days), HasData = true };
        }

        // No violations ever → streak = days since first plan was created.
        var firstPlanAt = allPlans.Min(p => p.CreatedAt).Date;
        var streak = Math.Max(0, (today - firstPlanAt).Days);
        return new DisciplineStreakDto { DaysWithoutViolation = streak, HasData = true };
    }

    /// <summary>
    /// Returns the exit TradeDate if plan was a loss trade that violated SL; null otherwise.
    /// Mirrors logic in DisciplineScoreCalculator.ComputeSlIntegrityAndStopHonor.
    /// </summary>
    private static DateTime? DetectViolation(TradePlan plan, List<Trade> trades)
    {
        var isBuy = plan.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);

        var entries = trades.Where(t => isBuy ? t.TradeType == TradeType.BUY : t.TradeType == TradeType.SELL).ToList();
        var exits = trades.Where(t => isBuy ? t.TradeType == TradeType.SELL : t.TradeType == TradeType.BUY).ToList();
        if (entries.Count == 0 || exits.Count == 0) return null;

        var entryQty = entries.Sum(t => t.Quantity);
        var exitQty = exits.Sum(t => t.Quantity);
        if (entryQty == 0 || exitQty == 0) return null;

        var avgEntry = entries.Sum(t => t.Price * t.Quantity) / entryQty;
        var avgExit = exits.Sum(t => t.Price * t.Quantity) / exitQty;

        var isLoss = isBuy ? avgExit < avgEntry : avgExit > avgEntry;
        if (!isLoss) return null;

        var honored = isBuy ? avgExit >= plan.StopLoss : avgExit <= plan.StopLoss;
        if (honored) return null;

        return exits.Max(t => t.TradeDate);
    }
}
