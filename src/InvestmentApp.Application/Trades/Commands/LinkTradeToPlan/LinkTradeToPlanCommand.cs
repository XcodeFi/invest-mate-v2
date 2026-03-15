using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Trades.Commands.LinkTradeToPlan;

public class LinkTradeToPlanCommand : IRequest<bool>
{
    public string TradeId { get; set; } = null!;
    public string PlanId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class LinkTradeToPlanCommandHandler : IRequestHandler<LinkTradeToPlanCommand, bool>
{
    private readonly ITradeRepository _tradeRepository;
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public LinkTradeToPlanCommandHandler(
        ITradeRepository tradeRepository,
        ITradePlanRepository tradePlanRepository,
        IPortfolioRepository portfolioRepository)
    {
        _tradeRepository = tradeRepository;
        _tradePlanRepository = tradePlanRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<bool> Handle(LinkTradeToPlanCommand request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null) return false;

        // Verify user owns this trade's portfolio
        var portfolio = await _portfolioRepository.GetByIdAsync(trade.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId) return false;

        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId) return false;

        // Link trade → plan
        trade.LinkTradePlan(plan.Id);
        await _tradeRepository.UpdateAsync(trade, cancellationToken);

        // Link plan → trade (if not already linked)
        if (plan.TradeId == null && plan.TradeIds?.Contains(trade.Id) != true)
        {
            plan.Execute(trade.Id);
            await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        }

        return true;
    }
}
