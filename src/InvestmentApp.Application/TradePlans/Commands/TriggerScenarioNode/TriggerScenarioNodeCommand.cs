using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.TriggerScenarioNode;

public class TriggerScenarioNodeCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    [JsonIgnore]
    public string NodeId { get; set; } = null!;
    public string? TradeId { get; set; }
}

public class TriggerScenarioNodeCommandHandler : IRequestHandler<TriggerScenarioNodeCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public TriggerScenarioNodeCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<Unit> Handle(TriggerScenarioNodeCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        plan.TriggerScenarioNode(request.NodeId, request.TradeId);

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
