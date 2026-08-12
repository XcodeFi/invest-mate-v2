using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/market-closures")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MarketClosuresController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketClosuresController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    public record AddRequest(List<DateTime> Dates, string? Note);

    [HttpGet]
    [ProducesResponseType(typeof(MarketClosureYearDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int year)
        => Ok(await _mediator.Send(new GetMarketClosuresQuery(GetUserId(), year)));

    [HttpPost]
    [ProducesResponseType(typeof(AddMarketClosuresResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] AddRequest request)
        => Ok(await _mediator.Send(new AddMarketClosuresCommand(GetUserId(), request.Dates, request.Note)));

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(DateTime date)
        => await _mediator.Send(new RemoveMarketClosureCommand(GetUserId(), date))
            ? NoContent()
            : NotFound();
}
