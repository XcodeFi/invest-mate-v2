using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.ValueObjects;
using MediatR;

namespace InvestmentApp.Application.Currency.Queries.ConvertCurrency;

public record ConvertCurrencyQuery(decimal Amount, string FromCurrency, string ToCurrency) : IRequest<ConvertCurrencyResult>;

public record ConvertCurrencyResult(decimal OriginalAmount, string FromCurrency, decimal ConvertedAmount, string ToCurrency, decimal Rate);

public class ConvertCurrencyQueryHandler : IRequestHandler<ConvertCurrencyQuery, ConvertCurrencyResult>
{
    private readonly ICurrencyService _currencyService;

    public ConvertCurrencyQueryHandler(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    public async Task<ConvertCurrencyResult> Handle(ConvertCurrencyQuery request, CancellationToken cancellationToken)
    {
        var rate = await _currencyService.GetExchangeRateAsync(request.FromCurrency, request.ToCurrency, cancellationToken);
        var source = new Money(request.Amount, request.FromCurrency);
        var converted = source.ConvertTo(request.ToCurrency, rate);
        return new ConvertCurrencyResult(request.Amount, request.FromCurrency, converted.Amount, request.ToCurrency, rate);
    }
}
