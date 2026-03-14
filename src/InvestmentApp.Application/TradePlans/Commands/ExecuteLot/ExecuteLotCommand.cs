using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.ExecuteLot;

public class ExecuteLotCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    [JsonIgnore]
    public int LotNumber { get; set; }
    public string TradeId { get; set; } = null!;
    public decimal ActualPrice { get; set; }
}

public class ExecuteLotCommandHandler : IRequestHandler<ExecuteLotCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;

    public ExecuteLotCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<Unit> Handle(ExecuteLotCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        plan.ExecuteLot(request.LotNumber, request.TradeId, request.ActualPrice);

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
