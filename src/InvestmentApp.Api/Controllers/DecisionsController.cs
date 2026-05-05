using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
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

    /// <summary>
    /// Resolve 1 DecisionItem inline: BÁN theo plan hoặc GIỮ + ghi lý do (≥ 20 ký tự).
    /// (P4 — Decision Engine v1.1 — xem `docs/plans/dashboard-decision-engine.md` §6)
    /// </summary>
    [HttpPost("{id}/resolve")]
    [ProducesResponseType(typeof(ResolveDecisionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(string id, [FromBody] ResolveDecisionRequest? request, CancellationToken ct)
    {
        // ConfigureApiBehaviorOptions.SuppressModelStateInvalidFilter=true means a body that
        // fails to deserialize lets `request` arrive as null instead of auto-400. Surface a
        // useful 400 instead of dereferencing → NRE → 500.
        if (request is null)
            return BadRequest(new { error = "Body request không hợp lệ — kiểm tra Action/TradePlanId/Symbol/Note." });

        var command = new ResolveDecisionCommand
        {
            DecisionId = id,
            Action = request.Action,
            TradePlanId = request.TradePlanId,
            Symbol = request.Symbol,
            Note = request.Note,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

public class ResolveDecisionRequest
{
    public DecisionAction Action { get; set; }
    public string? TradePlanId { get; set; }
    public string? Symbol { get; set; }
    public string? Note { get; set; }
}
