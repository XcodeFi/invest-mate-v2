using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/decisions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DecisionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DecisionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Decision Queue cho Dashboard — gộp StopLoss + Scenario advisory + Thesis review
    /// thành 1 list duy nhất, sort theo severity desc. (P3 — Decision Engine v1.1)
    /// </summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(DecisionQueueDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        var query = new GetDecisionQueueQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
