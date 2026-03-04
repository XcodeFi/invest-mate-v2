using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Interfaces;

public interface IStockPriceService
{
    Task<Money> GetCurrentPriceAsync(StockSymbol symbol);
    Task<Dictionary<string, Money>> GetCurrentPricesAsync(IEnumerable<StockSymbol> symbols);
}