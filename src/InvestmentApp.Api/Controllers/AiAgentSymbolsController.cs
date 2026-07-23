using InvestmentApp.Api.Auth;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Sự kiện theo mã (timeline) cho agent (scheme ApiKey). Mirror SymbolTimelineController.</summary>
[ApiController]
[Route("api/v1/ai/agent/symbols")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentSymbolsController : AiAgentControllerBase
{
    public AiAgentSymbolsController(IMediator mediator) : base(mediator) { }

    [HttpGet("{symbol}/timeline")]
    [ProducesResponseType(typeof(SymbolTimelineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSymbolTimeline(
        string symbol, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = new GetSymbolTimelineQuery
        {
            UserId = GetUserId(),
            Symbol = symbol,
            From = from,
            To = to
        };
        return Ok(await _mediator.Send(query));
    }
}
