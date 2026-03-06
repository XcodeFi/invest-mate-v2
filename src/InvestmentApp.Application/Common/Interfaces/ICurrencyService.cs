using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICurrencyService
{
    Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    Task<Money> ConvertAsync(Money amount, string targetCurrency, CancellationToken cancellationToken = default);
    Task<Dictionary<string, decimal>> GetAllRatesAsync(string baseCurrency, CancellationToken cancellationToken = default);
    Task RefreshRatesAsync(CancellationToken cancellationToken = default);
}
