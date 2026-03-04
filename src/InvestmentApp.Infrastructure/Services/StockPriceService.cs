using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Infrastructure.Services;

public class StockPriceService : IStockPriceService
{
    // Mock stock prices - in production, this would call a real stock price API
    private static readonly Dictionary<string, decimal> _mockPrices = new()
    {
        { "AAPL", 150.25m },
        { "GOOGL", 2750.80m },
        { "MSFT", 305.50m },
        { "TSLA", 245.75m },
        { "AMZN", 3380.90m },
        { "NVDA", 875.30m },
        { "META", 330.45m },
        { "NFLX", 485.20m }
    };

    private static readonly Random _random = new();

    public Task<Money> GetCurrentPriceAsync(StockSymbol symbol)
    {
        // In production, this would call a real stock price API
        // For demo purposes, return mock data with some randomization
        var basePrice = _mockPrices.GetValueOrDefault(symbol.Value, 100.00m);
        var variation = (decimal)(_random.NextDouble() * 0.1 - 0.05); // ±5% variation
        var currentPrice = basePrice * (1 + variation);

        return Task.FromResult(new Money(Math.Round(currentPrice, 2), "USD"));
    }

    public async Task<Dictionary<string, Money>> GetCurrentPricesAsync(IEnumerable<StockSymbol> symbols)
    {
        var prices = new Dictionary<string, Money>();

        foreach (var symbol in symbols)
        {
            prices[symbol.Value] = await GetCurrentPriceAsync(symbol);
        }

        return prices;
    }
}