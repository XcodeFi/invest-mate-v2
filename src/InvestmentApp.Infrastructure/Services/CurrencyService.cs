using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Currency conversion service backed by exchange_rates collection.
/// Rates are refreshed by ExchangeRateJob (Worker). Falls back to
/// hardcoded VND/USD baseline when no DB record exists.
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly IExchangeRateRepository _rateRepository;
    private readonly ILogger<CurrencyService> _logger;

    // Fallback rates relative to USD (used only when DB has no data)
    private static readonly Dictionary<string, decimal> _fallbackUsdRates = new()
    {
        ["VND"] = 25450m,
        ["USD"] = 1m,
        ["EUR"] = 0.92m,
        ["USDT"] = 1m,
    };

    public CurrencyService(IExchangeRateRepository rateRepository, ILogger<CurrencyService> logger)
    {
        _rateRepository = rateRepository;
        _logger = logger;
    }

    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        if (from == to) return 1m;

        // Try direct rate
        var rate = await _rateRepository.GetLatestAsync(from, to, cancellationToken);
        if (rate != null) return rate.Rate;

        // Try inverse
        var inverse = await _rateRepository.GetLatestAsync(to, from, cancellationToken);
        if (inverse != null) return 1m / inverse.Rate;

        // Fallback via USD cross-rate
        _logger.LogWarning("No DB exchange rate for {From}/{To}, using fallback", from, to);
        return GetFallbackRate(from, to);
    }

    public async Task<Money> ConvertAsync(Money amount, string targetCurrency, CancellationToken cancellationToken = default)
    {
        var rate = await GetExchangeRateAsync(amount.Currency, targetCurrency, cancellationToken);
        return amount.ConvertTo(targetCurrency, rate);
    }

    public async Task<Dictionary<string, decimal>> GetAllRatesAsync(string baseCurrency, CancellationToken cancellationToken = default)
    {
        var rates = await _rateRepository.GetAllLatestAsync(baseCurrency, cancellationToken);
        return rates.ToDictionary(r => r.TargetCurrency, r => r.Rate);
    }

    public async Task RefreshRatesAsync(CancellationToken cancellationToken = default)
    {
        // Seed fallback rates into DB if collection is empty
        var existing = await _rateRepository.GetAllLatestAsync("USD", cancellationToken);
        if (!existing.Any())
        {
            _logger.LogInformation("Seeding fallback exchange rates into DB");
            foreach (var (target, rate) in _fallbackUsdRates)
            {
                if (target == "USD") continue;
                var entity = new ExchangeRate("USD", target, rate, DateTime.UtcNow.Date, "fallback-seed");
                await _rateRepository.UpsertAsync(entity, cancellationToken);
            }
        }
    }

    private static decimal GetFallbackRate(string from, string to)
    {
        var fromUsd = _fallbackUsdRates.GetValueOrDefault(from, 1m);
        var toUsd = _fallbackUsdRates.GetValueOrDefault(to, 1m);
        return toUsd / fromUsd;
    }
}
