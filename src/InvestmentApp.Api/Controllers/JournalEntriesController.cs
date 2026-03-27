using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/journal-entries")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class JournalEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalEntriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJournalEntry([FromBody] CreateJournalEntryCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/journal-entries/{id}", new { id });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateJournalEntry(string id, [FromBody] UpdateJournalEntryCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJournalEntry(string id)
    {
        var command = new DeleteJournalEntryCommand { Id = id, UserId = GetUserId() };
        var result = await _mediator.Send(command);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] string? symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "Symbol is required" });

        var query = new GetJournalEntriesBySymbolQuery
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
