using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;

public class UpdateTradePlanStatusCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? TradeId { get; set; }
}

public class UpdateTradePlanStatusCommandHandler : IRequestHandler<UpdateTradePlanStatusCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;

    public UpdateTradePlanStatusCommandHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<Unit> Handle(UpdateTradePlanStatusCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Trade plan {request.Id} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        switch (request.Status.ToLower())
        {
            case "ready":
                plan.MarkReady();
                break;
            case "inprogress":
            case "in_progress":
                plan.MarkInProgress();
                break;
            case "executed":
                if (string.IsNullOrEmpty(request.TradeId))
                    throw new ArgumentException("TradeId is required when marking as executed");
                plan.Execute(request.TradeId);
                // Link trade back to plan
                var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
                if (trade != null)
                {
                    trade.LinkTradePlan(plan.Id);
                    await _tradeRepository.UpdateAsync(trade, cancellationToken);
                }
                break;
            case "reviewed":
                plan.MarkReviewed();
                break;
            case "cancelled":
                plan.Cancel();
                break;
            default:
                throw new ArgumentException($"Invalid status: {request.Status}");
        }

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
