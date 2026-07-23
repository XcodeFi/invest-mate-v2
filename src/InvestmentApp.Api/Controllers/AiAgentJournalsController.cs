using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Nhật ký theo trade cho agent (scheme ApiKey). Mirror JournalsController.</summary>
[ApiController]
[Route("api/v1/ai/agent/journals")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentJournalsController : AiAgentControllerBase
{
    public AiAgentJournalsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetJournals([FromQuery] string? portfolioId)
        => Ok(await _mediator.Send(new GetJournalsQuery { UserId = GetUserId(), PortfolioId = portfolioId }));

    [HttpGet("trade/{tradeId}")]
    public async Task<IActionResult> GetJournalByTrade(string tradeId)
    {
        var result = await _mediator.Send(new GetJournalByTradeQuery { TradeId = tradeId, UserId = GetUserId() });
        return result == null ? NotFound(new { message = "No journal found for this trade" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateJournal([FromBody] CreateJournalCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/ai/agent/journals/{id}", new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateJournal(string id, [FromBody] UpdateJournalCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJournal(string id)
    {
        await _mediator.Send(new DeleteJournalCommand { Id = id, UserId = GetUserId() });
        return NoContent();
    }
}
