using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using System.Linq;

namespace InvestmentApp.Infrastructure.Services;

public class PnLService : IPnLService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IStockPriceService _stockPriceService;

    public PnLService(
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IStockPriceService stockPriceService)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _stockPriceService = stockPriceService;
    }

    public async Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId);
        var positionPnLs = new List<PositionPnL>();

        // Group trades by symbol
        var tradesBySymbol = trades.GroupBy(t => t.Symbol);

        foreach (var symbolGroup in tradesBySymbol)
        {
            var positionPnL = await CalculatePositionPnLAsync(portfolioId, new StockSymbol(symbolGroup.Key), cancellationToken);
            positionPnLs.Add(positionPnL);
        }

        var totalRealizedPnL = positionPnLs.Sum(p => 0m); // TODO: Calculate realized P&L
        var totalUnrealizedPnL = positionPnLs.Sum(p => p.UnrealizedPnL);
        var totalPortfolioValue = positionPnLs.Sum(p => p.MarketValue);
        var totalInvested = positionPnLs.Sum(p => p.Quantity * p.AverageCost);

        return new PortfolioPnLSummary
        {
            TotalRealizedPnL = totalRealizedPnL,
            TotalUnrealizedPnL = totalUnrealizedPnL,
            TotalPortfolioValue = totalPortfolioValue,
            TotalInvested = totalInvested
        };
    }

    public async Task<PositionPnL> CalculatePositionPnLAsync(string portfolioId, StockSymbol symbol, CancellationToken cancellationToken = default)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAndSymbolAsync(portfolioId, symbol.Value);

        if (!trades.Any())
            throw new ArgumentException($"No trades found for symbol {symbol.Value} in portfolio {portfolioId}");

        // Calculate average cost using FIFO method
        var (quantity, averageCost, realizedPnL) = CalculateAverageCostAndRealizedPnL(trades);

        // Get current price
        var currentPrice = await _stockPriceService.GetCurrentPriceAsync(symbol);
        var currentValue = new Money(quantity * currentPrice.Amount, currentPrice.Currency);
        var costBasis = new Money(quantity * averageCost.Amount, averageCost.Currency);
        var unrealizedPnL = new Money(currentValue.Amount - costBasis.Amount, currentPrice.Currency);

        return new PositionPnL
        {
            Symbol = symbol.Value,
            Quantity = quantity,
            AverageCost = averageCost.Amount,
            CurrentPrice = currentPrice.Amount
        };
    }

    public async Task UpdatePortfolioPositionsAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement position tracking logic
        // For now, this is a placeholder
    }

    private (decimal quantity, Money averageCost, Money realizedPnL) CalculateAverageCostAndRealizedPnL(IEnumerable<Trade> trades)
    {
        var buyTrades = trades.Where(t => t.TradeType == TradeType.BUY).OrderBy(t => t.CreatedAt).ToList();
        var sellTrades = trades.Where(t => t.TradeType == TradeType.SELL).OrderBy(t => t.CreatedAt).ToList();

        decimal totalQuantity = 0;
        decimal totalCost = 0;
        decimal realizedPnL = 0;

        // Process buy trades to calculate average cost
        foreach (var buy in buyTrades)
        {
            totalCost += buy.Quantity * buy.Price;
            totalQuantity += buy.Quantity;
        }

        var averageCost = totalQuantity > 0 ? new Money(totalCost / totalQuantity, "USD") : new Money(0, "USD");

        // Process sell trades to calculate realized P&L
        foreach (var sell in sellTrades)
        {
            // For simplicity, assume sells are at average cost
            // In a real implementation, you'd use specific lot matching
            realizedPnL += sell.Quantity * (sell.Price - averageCost.Amount);
            totalQuantity -= sell.Quantity;
        }

        return (totalQuantity, averageCost, new Money(realizedPnL, "USD"));
    }
}