using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Mock market data provider that simulates Vietnamese stock market data.
/// In production, replace with SSI or FiinTrade API integration.
/// </summary>
public class MockMarketDataProvider : IMarketDataProvider
{
    private readonly ILogger<MockMarketDataProvider> _logger;
    private static readonly Random _random = new();

    // Vietnamese stock mock data (prices in VND)
    private static readonly Dictionary<string, decimal> _vnStockPrices = new()
    {
        { "VNM", 78000m }, { "FPT", 125000m }, { "VCB", 92000m },
        { "VHM", 45000m }, { "VIC", 42000m },  { "HPG", 26000m },
        { "MBB", 22000m }, { "TCB", 35000m },  { "VPB", 20000m },
        { "MSN", 68000m }, { "VRE", 28000m },  { "BVH", 48000m },
        { "PLX", 38000m }, { "GAS", 85000m },  { "SAB", 165000m },
        { "MWG", 55000m }, { "PNJ", 98000m },  { "REE", 62000m },
        // US stocks
        { "AAPL", 150.25m }, { "GOOGL", 175.80m }, { "MSFT", 420.50m },
        { "TSLA", 245.75m }, { "AMZN", 185.90m },  { "NVDA", 875.30m }
    };

    public MockMarketDataProvider(ILogger<MockMarketDataProvider> logger)
    {
        _logger = logger;
    }

    public Task<StockPriceData?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();
        var basePrice = _vnStockPrices.GetValueOrDefault(symbol, 50000m);
        var variation = (decimal)(_random.NextDouble() * 0.06 - 0.03); // ±3% variation
        var close = Math.Round(basePrice * (1 + variation), 0);
        var open = Math.Round(basePrice * (1 + (decimal)(_random.NextDouble() * 0.02 - 0.01)), 0);
        var high = Math.Max(open, close) + Math.Round((decimal)_random.NextDouble() * basePrice * 0.02m, 0);
        var low = Math.Min(open, close) - Math.Round((decimal)_random.NextDouble() * basePrice * 0.02m, 0);

        var data = new StockPriceData
        {
            Symbol = symbol,
            Date = DateTime.UtcNow.Date,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = _random.Next(100000, 5000000)
        };

        _logger.LogDebug("Fetched mock price for {Symbol}: {Close}", symbol, close);
        return Task.FromResult<StockPriceData?>(data);
    }

    public Task<List<StockPriceData>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();
        var basePrice = _vnStockPrices.GetValueOrDefault(symbol, 50000m);
        var results = new List<StockPriceData>();
        var currentPrice = basePrice * 0.9m; // Start 10% lower

        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            var dailyChange = (decimal)(_random.NextDouble() * 0.06 - 0.03);
            currentPrice *= (1 + dailyChange);
            currentPrice = Math.Max(currentPrice, basePrice * 0.5m);

            var open = Math.Round(currentPrice * (1 + (decimal)(_random.NextDouble() * 0.01 - 0.005)), 0);
            var close = Math.Round(currentPrice, 0);
            var high = Math.Max(open, close) + Math.Round((decimal)_random.NextDouble() * currentPrice * 0.015m, 0);
            var low = Math.Min(open, close) - Math.Round((decimal)_random.NextDouble() * currentPrice * 0.015m, 0);

            results.Add(new StockPriceData
            {
                Symbol = symbol,
                Date = date,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = _random.Next(100000, 5000000)
            });
        }

        return Task.FromResult(results);
    }

    public async Task<Dictionary<string, StockPriceData>> GetBatchPricesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, StockPriceData>();
        foreach (var symbol in symbols)
        {
            var price = await GetCurrentPriceAsync(symbol, cancellationToken);
            if (price != null)
                result[symbol.ToUpper()] = price;
        }
        return result;
    }

    public Task<MarketIndexData?> GetIndexDataAsync(string indexSymbol, CancellationToken cancellationToken = default)
    {
        indexSymbol = indexSymbol.ToUpper().Trim();

        var baseValues = new Dictionary<string, decimal>
        {
            { "VNINDEX", 1250.5m },
            { "VN30", 1280.3m },
            { "HNX", 230.5m }
        };

        var baseValue = baseValues.GetValueOrDefault(indexSymbol, 1000m);
        var variation = (decimal)(_random.NextDouble() * 0.04 - 0.02);
        var close = Math.Round(baseValue * (1 + variation), 2);
        var open = Math.Round(baseValue * (1 + (decimal)(_random.NextDouble() * 0.01 - 0.005)), 2);

        var data = new MarketIndexData
        {
            IndexSymbol = indexSymbol,
            Date = DateTime.UtcNow.Date,
            Open = open,
            High = Math.Max(open, close) + Math.Round((decimal)(_random.NextDouble() * 5), 2),
            Low = Math.Min(open, close) - Math.Round((decimal)(_random.NextDouble() * 5), 2),
            Close = close,
            Volume = _random.Next(500000000, 1500000000),
            Change = Math.Round(close - open, 2),
            ChangePercent = Math.Round(((close - open) / open) * 100, 2)
        };

        return Task.FromResult<MarketIndexData?>(data);
    }
}
