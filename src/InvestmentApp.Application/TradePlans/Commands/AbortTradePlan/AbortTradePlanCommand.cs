using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.AbortTradePlan;

/// <summary>
/// Abort plan vì thesis đã sai (§D4 plan Vin-discipline).
/// Khác với Cancel() ở chỗ bắt buộc ghi trigger + detail để học pattern DisciplinedAbort vs SunkCostHold.
/// Áp cho state Ready | InProgress | Executed (multi-lot partial).
/// </summary>
public class AbortTradePlanCommand : IRequest<AbortTradePlanResult>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;

    [JsonIgnore]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Loại trigger: EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual.
    /// </summary>
    public string Trigger { get; set; } = null!;

    /// <summary>
    /// Mô tả chi tiết thesis sai ở đâu (tối thiểu 20 ký tự).
    /// </summary>
    public string Detail { get; set; } = null!;
}

public class AbortTradePlanResult
{
    public string PlanId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Trigger { get; set; } = null!;
    public IList<string> TradeIdsAffected { get; set; } = new List<string>();
    public DateTime AbortedAt { get; set; }
}

public class AbortTradePlanCommandHandler : IRequestHandler<AbortTradePlanCommand, AbortTradePlanResult>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public AbortTradePlanCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<AbortTradePlanResult> Handle(AbortTradePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to abort this trade plan");

        if (!Enum.TryParse<InvalidationTrigger>(request.Trigger, ignoreCase: true, out var trigger))
            throw new ArgumentException(
                $"Invalid trigger '{request.Trigger}'. Valid: EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual.",
                nameof(request.Trigger));

        plan.AbortWithThesisInvalidation(trigger, request.Detail);
        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);

        var tradeIdsAffected = plan.TradeIds?.ToList()
            ?? (plan.TradeId != null ? new List<string> { plan.TradeId } : new List<string>());

        return new AbortTradePlanResult
        {
            PlanId = plan.Id,
            Status = plan.Status.ToString(),
            Trigger = trigger.ToString(),
            TradeIdsAffected = tradeIdsAffected,
            AbortedAt = DateTime.UtcNow
        };
    }
}
