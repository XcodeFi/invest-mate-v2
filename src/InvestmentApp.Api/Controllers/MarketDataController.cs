using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetBatchPrices;
using InvestmentApp.Application.MarketData.Queries.GetMarketIndex;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/market")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MarketDataController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketDataController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get current price for a stock symbol
    /// </summary>
    [HttpGet("price/{symbol}")]
    [ProducesResponseType(typeof(StockPriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentPrice(string symbol)
    {
        try
        {
            var query = new GetStockPriceQuery { Symbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"No price data found for symbol {symbol}" });
        }
    }

    /// <summary>
    /// Get historical prices for a stock symbol
    /// </summary>
    [HttpGet("price/{symbol}/history")]
    [ProducesResponseType(typeof(List<StockPriceHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceHistory(
        string symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = new GetStockPriceHistoryQuery
        {
            Symbol = symbol,
            From = from ?? DateTime.UtcNow.AddMonths(-3),
            To = to ?? DateTime.UtcNow
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get current prices for multiple symbols
    /// </summary>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(List<BatchPriceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchPrices([FromQuery] string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols))
            return BadRequest(new { message = "Symbols parameter is required" });

        var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var query = new GetBatchPricesQuery { Symbols = symbolList };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get market index data (VNINDEX, VN30, HNX)
    /// </summary>
    [HttpGet("index/{symbol}")]
    [ProducesResponseType(typeof(MarketIndexDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarketIndex(string symbol)
    {
        try
        {
            var query = new GetMarketIndexQuery { IndexSymbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"No data found for index {symbol}" });
        }
    }
}
