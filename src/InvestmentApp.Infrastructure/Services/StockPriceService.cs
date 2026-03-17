using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Bridges IStockPriceService (used by PnL/Risk/Positions) to IMarketDataProvider (24hmoney API).
/// Returns real-time prices in VND.
/// </summary>
public class StockPriceService : IStockPriceService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ILogger<StockPriceService> _logger;

    public StockPriceService(
        IMarketDataProvider marketDataProvider,
        ILogger<StockPriceService> logger)
    {
        _marketDataProvider = marketDataProvider;
        _logger = logger;
    }

    public async Task<Money> GetCurrentPriceAsync(StockSymbol symbol)
    {
        var priceData = await _marketDataProvider.GetCurrentPriceAsync(symbol.Value);

        if (priceData == null || priceData.Close <= 0)
        {
            _logger.LogWarning("No price data from API for {Symbol}, returning 0", symbol.Value);
            return new Money(0, "VND");
        }

        return new Money(priceData.Close, "VND");
    }

    public async Task<Dictionary<string, Money>> GetCurrentPricesAsync(IEnumerable<StockSymbol> symbols)
    {
        var symbolList = symbols.Select(s => s.Value).ToList();
        var batchPrices = await _marketDataProvider.GetBatchPricesAsync(symbolList);

        var result = new Dictionary<string, Money>();
        foreach (var (sym, priceData) in batchPrices)
        {
            result[sym] = new Money(priceData.Close, "VND");
        }

        // Fill missing symbols with 0
        foreach (var symbol in symbols)
        {
            if (!result.ContainsKey(symbol.Value))
            {
                _logger.LogWarning("No batch price for {Symbol}", symbol.Value);
                result[symbol.Value] = new Money(0, "VND");
            }
        }

        return result;
    }
}
