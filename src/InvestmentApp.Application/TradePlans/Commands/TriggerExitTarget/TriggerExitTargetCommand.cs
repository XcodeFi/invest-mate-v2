using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
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
    private readonly IPortfolioRepository _portfolioRepository;

    public TriggerExitTargetCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Unit> Handle(TriggerExitTargetCommand request, CancellationToken cancellationToken)
    {
        // Kế hoạch không có và kế hoạch của người khác cho cùng một phản hồi (KeyNotFound → 404)
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        // Kiểm quyền trade TRƯỚC khi đổi plan: bị từ chối thì plan không giữ liên kết nào
        var trade = await TradeOwnership.LoadOwnedAsync(request.TradeId, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken);

        plan.TriggerExitTarget(request.Level, request.TradeId);

        // Link trade back to plan
        if (trade != null)
        {
            trade.LinkTradePlan(plan.Id);
            await _tradeRepository.UpdateAsync(trade, cancellationToken);
        }

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
