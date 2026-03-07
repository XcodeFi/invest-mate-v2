using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Trades.Commands.CreateTrade;

public class CreateTradeCommand : IRequest<string>
{
    public string PortfolioId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public DateTime? TradeDate { get; set; }
}

public class CreateTradeCommandHandler : IRequestHandler<CreateTradeCommand, string>
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public CreateTradeCommandHandler(
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<string> Handle(CreateTradeCommand request, CancellationToken cancellationToken)
    {
        // Validate portfolio exists and belongs to user
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null)
            throw new InvalidOperationException("Portfolio not found");

        // Parse trade type
        if (!Enum.TryParse<TradeType>(request.TradeType, true, out var tradeType))
            throw new ArgumentException("Invalid trade type");

        var trade = new Trade(
            request.PortfolioId,
            request.Symbol,
            tradeType,
            request.Quantity,
            request.Price,
            request.Fee,
            request.Tax,
            request.TradeDate);

        await _tradeRepository.AddAsync(trade, cancellationToken);

        // Update portfolio with new trade
        portfolio.AddTrade(trade);
        await _portfolioRepository.UpdateAsync(portfolio, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = portfolio.UserId,
            Action = "CREATE_TRADE",
            EntityId = trade.Id,
            Metadata = new
            {
                request.Symbol,
                request.TradeType,
                request.Quantity,
                request.Price,
                request.Fee,
                request.Tax
            }
        }, cancellationToken);

        return trade.Id;
    }
}