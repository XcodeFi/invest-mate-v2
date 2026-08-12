using InvestmentApp.Api.Auth;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Lịch nghỉ giao dịch cho agent (scheme ApiKey). Mirror MarketClosuresController.</summary>
[ApiController]
[Route("api/v1/ai/agent/market-closures")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentMarketClosuresController : AiAgentControllerBase
{
    public AiAgentMarketClosuresController(IMediator mediator) : base(mediator) { }

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
