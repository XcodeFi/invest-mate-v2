using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
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
    private readonly IPortfolioRepository _portfolioRepository;

    public ExecuteLotCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Unit> Handle(ExecuteLotCommand request, CancellationToken cancellationToken)
    {
        // Kế hoạch không có và kế hoạch của người khác cho cùng một phản hồi (KeyNotFound → 404)
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        // Kiểm quyền trade TRƯỚC khi đổi plan: bị từ chối thì plan không giữ liên kết nào
        var trade = await TradeOwnership.LoadOwnedAsync(request.TradeId, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken);

        plan.ExecuteLot(request.LotNumber, request.TradeId, request.ActualPrice);

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
