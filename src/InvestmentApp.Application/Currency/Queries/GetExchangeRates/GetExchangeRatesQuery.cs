using InvestmentApp.Application.Common.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Currency.Queries.GetExchangeRates;

public record GetExchangeRatesQuery(string BaseCurrency = "USD") : IRequest<Dictionary<string, decimal>>;

public class GetExchangeRatesQueryHandler : IRequestHandler<GetExchangeRatesQuery, Dictionary<string, decimal>>
{
    private readonly ICurrencyService _currencyService;

    public GetExchangeRatesQueryHandler(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    public Task<Dictionary<string, decimal>> Handle(GetExchangeRatesQuery request, CancellationToken cancellationToken)
        => _currencyService.GetAllRatesAsync(request.BaseCurrency, cancellationToken);
}
