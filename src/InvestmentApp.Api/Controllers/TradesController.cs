using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Application.Trades.Commands.DeleteTrade;
using InvestmentApp.Application.Trades.Queries.GetTradesByPortfolio;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/trades")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TradesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TradesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Create a new trade
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTrade([FromBody] CreateTradeCommand command)
    {
        var tradeId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTrade), new { id = tradeId }, new { id = tradeId });
    }

    /// <summary>
    /// Get a specific trade by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrade(string id)
    {
        // For now return a simple response - can be expanded with a dedicated query
        return Ok(new { id });
    }

    /// <summary>
    /// Delete a trade
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTrade(string id)
    {
        var userId = GetUserId();
        var command = new DeleteTradeCommand { TradeId = id, UserId = userId };
        var result = await _mediator.Send(command);

        if (!result)
            return NotFound();

        return NoContent();
    }
}