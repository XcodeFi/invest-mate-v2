using InvestmentApp.Application.Currency.Queries.ConvertCurrency;
using InvestmentApp.Application.Currency.Queries.GetExchangeRates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/currency")]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurrencyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>GET /api/v1/currency/rates?base=USD — All exchange rates for base currency</summary>
    [HttpGet("rates")]
    public async Task<IActionResult> GetRates([FromQuery] string base_ = "USD", CancellationToken cancellationToken = default)
    {
        var rates = await _mediator.Send(new GetExchangeRatesQuery(base_), cancellationToken);
        return Ok(new { baseCurrency = base_.ToUpperInvariant(), rates });
    }

    /// <summary>GET /api/v1/currency/convert?from=USD&amp;to=VND&amp;amount=100 — Convert amount</summary>
    [HttpGet("convert")]
    public async Task<IActionResult> Convert(
        [FromQuery] decimal amount,
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return BadRequest("Amount must be positive");

        var result = await _mediator.Send(new ConvertCurrencyQuery(amount, from, to), cancellationToken);
        return Ok(result);
    }
}
