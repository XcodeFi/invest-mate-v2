using InvestmentApp.Application.MarketEvents.Commands.CreateMarketEvent;
using InvestmentApp.Application.MarketEvents.Commands.CrawlVietstockEvents;
using InvestmentApp.Application.MarketEvents.Queries.GetMarketEvents;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/market-events")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MarketEventsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketEventsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMarketEvent([FromBody] CreateMarketEventCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/market-events/{id}", new { id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<MarketEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMarketEvents(
        [FromQuery] string? symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "Symbol is required" });

        var query = new GetMarketEventsQuery
        {
            Symbol = symbol,
            From = from,
            To = to
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("crawl")]
    [ProducesResponseType(typeof(CrawlResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrawlVietstockEvents([FromBody] CrawlVietstockEventsCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Symbol))
            return BadRequest(new { error = "Symbol is required" });

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
