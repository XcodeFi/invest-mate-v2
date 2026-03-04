using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/journals")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class JournalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// List all journal entries for user, optionally filtered by portfolio
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JournalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJournals([FromQuery] string? portfolioId)
    {
        var query = new GetJournalsQuery
        {
            UserId = GetUserId(),
            PortfolioId = portfolioId
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get journal for a specific trade
    /// </summary>
    [HttpGet("trade/{tradeId}")]
    [ProducesResponseType(typeof(JournalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJournalByTrade(string tradeId)
    {
        var query = new GetJournalByTradeQuery
        {
            TradeId = tradeId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        if (result == null) return NotFound(new { message = "No journal found for this trade" });
        return Ok(result);
    }

    /// <summary>
    /// Create a journal entry for a trade
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJournal([FromBody] CreateJournalCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/journals/{id}", new { id });
    }

    /// <summary>
    /// Update an existing journal entry
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateJournal(string id, [FromBody] UpdateJournalCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a journal entry (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteJournal(string id)
    {
        var command = new DeleteJournalCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }
}
