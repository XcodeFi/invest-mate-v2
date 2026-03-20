using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/watchlists")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class WatchlistsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WatchlistsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get all watchlists for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetWatchlistsQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get watchlist detail with items
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(string id)
    {
        var query = new GetWatchlistDetailQuery { Id = id, UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a new watchlist
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWatchlistCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Created($"/api/v1/watchlists/{result.Id}", result);
    }

    /// <summary>
    /// Update watchlist info (name, emoji, sortOrder)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWatchlistCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a watchlist (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var command = new DeleteWatchlistCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Add a symbol to watchlist
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(string id, [FromBody] AddWatchlistItemCommand command)
    {
        command.WatchlistId = id;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Update a watchlist item (note, target prices)
    /// </summary>
    [HttpPut("{id}/items/{symbol}")]
    public async Task<IActionResult> UpdateItem(string id, string symbol,
        [FromBody] UpdateWatchlistItemCommand command)
    {
        command.WatchlistId = id;
        command.Symbol = symbol;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Remove a symbol from watchlist
    /// </summary>
    [HttpDelete("{id}/items/{symbol}")]
    public async Task<IActionResult> RemoveItem(string id, string symbol)
    {
        var command = new RemoveWatchlistItemCommand
        {
            WatchlistId = id,
            Symbol = symbol,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Import VN30 symbols into a watchlist (or create new VN30 watchlist)
    /// </summary>
    [HttpPost("import-vn30")]
    public async Task<IActionResult> ImportVn30([FromBody] ImportVn30Command command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
