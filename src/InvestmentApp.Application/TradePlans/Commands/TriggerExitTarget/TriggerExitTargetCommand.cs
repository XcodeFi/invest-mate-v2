using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;

public class TriggerExitTargetCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    [JsonIgnore]
    public int Level { get; set; }
    public string TradeId { get; set; } = null!;
}

public class TriggerExitTargetCommandHandler : IRequestHandler<TriggerExitTargetCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;

    public TriggerExitTargetCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<Unit> Handle(TriggerExitTargetCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        plan.TriggerExitTarget(request.Level, request.TradeId);

        // Link trade back to plan
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade != null)
        {
            trade.LinkTradePlan(plan.Id);
            await _tradeRepository.UpdateAsync(trade, cancellationToken);
        }

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
