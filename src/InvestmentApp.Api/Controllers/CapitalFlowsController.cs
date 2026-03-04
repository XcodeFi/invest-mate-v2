using InvestmentApp.Application.CapitalFlows.Commands.RecordCapitalFlow;
using InvestmentApp.Application.CapitalFlows.Commands.DeleteCapitalFlow;
using InvestmentApp.Application.CapitalFlows.Queries.GetFlowHistory;
using InvestmentApp.Application.CapitalFlows.Queries.GetAdjustedReturn;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/capital-flows")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CapitalFlowsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CapitalFlowsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Record a capital flow (deposit, withdrawal, dividend, etc.)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordCapitalFlow([FromBody] RecordCapitalFlowCommand command)
    {
        command.UserId = GetUserId();
        var flowId = await _mediator.Send(command);
        return Created($"/api/v1/capital-flows/{flowId}", new { id = flowId });
    }

    /// <summary>
    /// Get capital flow history for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}")]
    [ProducesResponseType(typeof(CapitalFlowHistoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlowHistory(
        string portfolioId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = new GetFlowHistoryQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            From = from,
            To = to
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get flow summary for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/summary")]
    [ProducesResponseType(typeof(CapitalFlowHistoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlowSummary(string portfolioId)
    {
        var query = new GetFlowHistoryQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(new
        {
            result.PortfolioId,
            result.TotalDeposits,
            result.TotalWithdrawals,
            result.TotalDividends,
            result.NetCashFlow,
            FlowCount = result.Flows.Count
        });
    }

    /// <summary>
    /// Get Time-Weighted Return (TWR) for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/twr")]
    [ProducesResponseType(typeof(AdjustedReturnDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeWeightedReturn(string portfolioId)
    {
        var query = new GetAdjustedReturnQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get Money-Weighted Return (MWR / IRR) for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/mwr")]
    [ProducesResponseType(typeof(AdjustedReturnDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMoneyWeightedReturn(string portfolioId)
    {
        var query = new GetAdjustedReturnQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Delete a capital flow record
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCapitalFlow(string id)
    {
        var command = new DeleteCapitalFlowCommand { Id = id, UserId = GetUserId() };
        var result = await _mediator.Send(command);
        if (!result)
            return NotFound();
        return NoContent();
    }
}
