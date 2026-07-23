using InvestmentApp.Api.Auth;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Watchlist CRUD cho agent (scheme ApiKey). Mirror WatchlistsController.</summary>
[ApiController]
[Route("api/v1/ai/agent/watchlists")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentWatchlistsController : AiAgentControllerBase
{
    public AiAgentWatchlistsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _mediator.Send(new GetWatchlistsQuery { UserId = GetUserId() }));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(string id)
        => Ok(await _mediator.Send(new GetWatchlistDetailQuery { Id = id, UserId = GetUserId() }));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWatchlistCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Created($"/api/v1/ai/agent/watchlists/{result.Id}", result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWatchlistCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _mediator.Send(new DeleteWatchlistCommand { Id = id, UserId = GetUserId() });
        return NoContent();
    }

    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(string id, [FromBody] AddWatchlistItemCommand command)
    {
        command.WatchlistId = id;
        command.UserId = GetUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpPut("{id}/items/{symbol}")]
    public async Task<IActionResult> UpdateItem(string id, string symbol,
        [FromBody] UpdateWatchlistItemCommand command)
    {
        command.WatchlistId = id;
        command.Symbol = symbol;
        command.UserId = GetUserId();
        return Ok(await _mediator.Send(command));
    }

    [HttpDelete("{id}/items/{symbol}")]
    public async Task<IActionResult> RemoveItem(string id, string symbol)
    {
        var command = new RemoveWatchlistItemCommand { WatchlistId = id, Symbol = symbol, UserId = GetUserId() };
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("import-vn30")]
    public async Task<IActionResult> ImportVn30([FromBody] ImportVn30Command command)
    {
        command.UserId = GetUserId();
        return Ok(await _mediator.Send(command));
    }
}
