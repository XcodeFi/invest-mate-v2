using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Strategies.Commands.LinkTrade;

public class LinkTradeToStrategyCommand : IRequest<Unit>
{
    public string StrategyId { get; set; } = null!;
    public string TradeId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class LinkTradeToStrategyCommandHandler : IRequestHandler<LinkTradeToStrategyCommand, Unit>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public LinkTradeToStrategyCommandHandler(IStrategyRepository strategyRepository, ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository)
    {
        _strategyRepository = strategyRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Unit> Handle(LinkTradeToStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new Exception($"Strategy {request.StrategyId} not found");

        if (strategy.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        var trade = await TradeOwnership.LoadOwnedAsync(request.TradeId, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken)
            ?? throw new Exception($"Trade {request.TradeId} not found");

        trade.LinkStrategy(request.StrategyId);
        await _tradeRepository.UpdateAsync(trade, cancellationToken);
        return Unit.Value;
    }
}
