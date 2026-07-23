using InvestmentApp.Api.Auth;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Nhật ký theo mã (standalone) cho agent (scheme ApiKey). Mirror JournalEntriesController.</summary>
[ApiController]
[Route("api/v1/ai/agent/journal-entries")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentJournalEntriesController : AiAgentControllerBase
{
    public AiAgentJournalEntriesController(IMediator mediator) : base(mediator) { }

    [HttpPost]
    public async Task<IActionResult> CreateJournalEntry([FromBody] CreateJournalEntryCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/ai/agent/journal-entries/{id}", new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateJournalEntry(string id, [FromBody] UpdateJournalEntryCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return result ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJournalEntry(string id)
    {
        var result = await _mediator.Send(new DeleteJournalEntryCommand { Id = id, UserId = GetUserId() });
        return result ? NoContent() : NotFound();
    }

    [HttpGet("pending-review")]
    public async Task<IActionResult> GetPendingReview([FromQuery] string? portfolioId)
        => Ok(await _mediator.Send(new GetTradesPendingReviewQuery { UserId = GetUserId(), PortfolioId = portfolioId }));

    [HttpGet]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] string? symbol, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
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
        return Ok(await _mediator.Send(query));
    }
}
