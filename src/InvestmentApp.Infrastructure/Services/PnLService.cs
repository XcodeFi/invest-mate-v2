using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Infrastructure.Services;

public class PnLService : IPnLService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IStockPriceService _stockPriceService;
    private readonly ICorporateActionRepository _corporateActionRepository;

    public PnLService(
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IStockPriceService stockPriceService,
        ICorporateActionRepository corporateActionRepository)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _stockPriceService = stockPriceService;
        _corporateActionRepository = corporateActionRepository;
    }

    public async Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var actions = await _corporateActionRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var positions = PositionBuilder.Build(trades, actions, DateTime.UtcNow);

        var results = new List<PositionPnL>();
        foreach (var position in positions)
        {
            var priced = await ToPositionPnLAsync(position);
            if (priced != null) results.Add(priced);
        }

        return new PortfolioPnLSummary
        {
            TotalRealizedPnL = results.Sum(p => p.RealizedPnL),
            TotalUnrealizedPnL = results.Sum(p => p.UnrealizedPnL),
            TotalPortfolioValue = results.Sum(p => p.MarketValue),
            TotalInvested = results.Sum(p => p.TotalCost),
            Positions = results
        };
    }

    public async Task<PositionPnL> CalculatePositionPnLAsync(string portfolioId, StockSymbol symbol, CancellationToken cancellationToken = default)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAndSymbolAsync(portfolioId, symbol.Value, cancellationToken);
        if (!trades.Any())
            throw new ArgumentException($"No trades found for symbol {symbol.Value} in portfolio {portfolioId}");

        var actions = await _corporateActionRepository.GetByPortfolioIdAndSymbolAsync(portfolioId, symbol.Value, cancellationToken);
        var position = PositionBuilder.Build(trades, actions, DateTime.UtcNow)
            .First(p => string.Equals(p.Symbol, symbol.Value, StringComparison.OrdinalIgnoreCase));

        return await ToPositionPnLAsync(position)
            ?? throw new ArgumentException($"Price not available for symbol {symbol.Value}");
    }

    public Task UpdatePortfolioPositionsAsync(string portfolioId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>Trả về null khi không lấy được giá, để một mã hỏng không làm hỏng cả danh mục.</summary>
    private async Task<PositionPnL?> ToPositionPnLAsync(AdjustedPosition position)
    {
        decimal currentPrice;
        try
        {
            var price = await _stockPriceService.GetCurrentPriceAsync(new StockSymbol(position.Symbol));
            currentPrice = price.Amount;
        }
        catch
        {
            return null;
        }

        return new PositionPnL
        {
            Symbol = position.Symbol,
            Quantity = position.TotalQuantity,
            SettledQuantity = position.SettledQuantity,
            PendingQuantity = position.PendingQuantity,
            AverageCost = position.AverageCost,
            CurrentPrice = currentPrice,
            RealizedPnL = position.RealizedPnL,
            DividendNet = position.DividendNet,
            PendingDividend = position.PendingDividend
        };
    }
}
