using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;

public class UpdateStopLossCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public decimal NewStopLoss { get; set; }
    public string? Reason { get; set; }
}

public class UpdateStopLossCommandHandler : IRequestHandler<UpdateStopLossCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public UpdateStopLossCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<Unit> Handle(UpdateStopLossCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        plan.UpdateStopLossWithHistory(request.NewStopLoss, request.Reason);

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
