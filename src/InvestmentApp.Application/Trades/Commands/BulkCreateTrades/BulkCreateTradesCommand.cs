using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Trades.Commands.BulkCreateTrades;

public class BulkTradeItem
{
    public string Symbol { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public DateTime? TradeDate { get; set; }
}

public class BulkCreateTradesCommand : IRequest<BulkCreateTradesResult>
{
    public string PortfolioId { get; set; } = null!;
    public List<BulkTradeItem> Trades { get; set; } = new();
}

public class BulkCreateTradesResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> CreatedIds { get; set; } = new();
}

public class BulkCreateTradesCommandHandler : IRequestHandler<BulkCreateTradesCommand, BulkCreateTradesResult>
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public BulkCreateTradesCommandHandler(ITradeRepository tradeRepository, IPortfolioRepository portfolioRepository)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<BulkCreateTradesResult> Handle(BulkCreateTradesCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken)
            ?? throw new InvalidOperationException("Portfolio not found");

        var result = new BulkCreateTradesResult();

        for (int i = 0; i < request.Trades.Count; i++)
        {
            var item = request.Trades[i];
            try
            {
                if (!Enum.TryParse<TradeType>(item.TradeType, true, out var tradeType))
                    throw new ArgumentException($"Invalid trade type: {item.TradeType}");

                var trade = new Trade(
                    request.PortfolioId,
                    item.Symbol.ToUpper(),
                    tradeType,
                    item.Quantity,
                    item.Price,
                    item.Fee,
                    item.Tax,
                    item.TradeDate);

                await _tradeRepository.AddAsync(trade, cancellationToken);
                portfolio.AddTrade(trade);
                result.CreatedIds.Add(trade.Id);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Dòng {i + 1} ({item.Symbol}): {ex.Message}");
            }
        }

        if (result.SuccessCount > 0)
        {
            await _portfolioRepository.UpdateAsync(portfolio, cancellationToken);
        }

        return result;
    }
}
