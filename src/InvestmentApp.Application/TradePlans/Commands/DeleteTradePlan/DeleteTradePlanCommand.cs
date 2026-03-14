using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.DeleteTradePlan;

public class DeleteTradePlanCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class DeleteTradePlanCommandHandler : IRequestHandler<DeleteTradePlanCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public DeleteTradePlanCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<Unit> Handle(DeleteTradePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Trade plan {request.Id} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to delete this trade plan");

        plan.SoftDelete();
        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
