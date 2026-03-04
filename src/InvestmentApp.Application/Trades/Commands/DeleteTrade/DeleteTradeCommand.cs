using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Trades.Commands.DeleteTrade;

public class DeleteTradeCommand : IRequest<bool>
{
    public string TradeId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteTradeCommandHandler : IRequestHandler<DeleteTradeCommand, bool>
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public DeleteTradeCommandHandler(
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<bool> Handle(DeleteTradeCommand request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
            return false;

        // Verify user owns the portfolio
        var portfolio = await _portfolioRepository.GetByIdAsync(trade.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return false;

        await _tradeRepository.DeleteAsync(request.TradeId, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "DELETE_TRADE",
            EntityId = trade.Id,
            EntityType = "Trade",
            Description = $"Trade deleted: {trade.Symbol} {trade.TradeType} {trade.Quantity}@{trade.Price}",
            Metadata = new
            {
                trade.Symbol,
                TradeType = trade.TradeType.ToString(),
                trade.Quantity,
                trade.Price
            }
        }, cancellationToken);

        return true;
    }
}
