using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/symbols")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SymbolTimelineController : ControllerBase
{
    private readonly IMediator _mediator;

    public SymbolTimelineController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet("{symbol}/timeline")]
    [ProducesResponseType(typeof(SymbolTimelineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSymbolTimeline(
        string symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = new GetSymbolTimelineQuery
        {
            UserId = GetUserId(),
            Symbol = symbol,
            From = from,
            To = to
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
