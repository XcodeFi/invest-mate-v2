using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Events;

/// <summary>
/// Raise khi user/hệ thống abort plan vì thesis đã sai (§D4 plan Vin-discipline).
/// Phục vụ P7 Behavioral Pattern Detection (DisciplinedAbort vs SunkCostHold)
/// và cache invalidation cho Discipline Score widget (§D6).
/// </summary>
public class TradePlanThesisInvalidatedEvent : IDomainEvent
{
    public string TradePlanId { get; }
    public string UserId { get; }
    public InvalidationTrigger Trigger { get; }
    public string Detail { get; }
    public DateTime AbortedAt { get; }
    public IReadOnlyList<string> TradeIds { get; }
    public DateTime OccurredOn { get; }

    public TradePlanThesisInvalidatedEvent(
        string tradePlanId,
        string userId,
        InvalidationTrigger trigger,
        string detail,
        IReadOnlyList<string>? tradeIds = null)
    {
        TradePlanId = tradePlanId;
        UserId = userId;
        Trigger = trigger;
        Detail = detail;
        AbortedAt = DateTime.UtcNow;
        TradeIds = tradeIds ?? Array.Empty<string>();
        OccurredOn = DateTime.UtcNow;
    }
}
