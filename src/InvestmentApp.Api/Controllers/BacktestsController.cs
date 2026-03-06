using InvestmentApp.Application.Backtests.Commands.RunBacktest;
using InvestmentApp.Application.Backtests.Queries.GetBacktest;
using InvestmentApp.Application.Backtests.Queries.GetBacktests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/backtests")]
[Authorize]
public class BacktestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BacktestsController(IMediator mediator) => _mediator = mediator;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();

    /// <summary>POST /api/v1/backtests — Queue a new backtest (processed async by Worker)</summary>
    [HttpPost]
    public async Task<IActionResult> RunBacktest([FromBody] RunBacktestRequest request, CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new RunBacktestCommand(
            UserId, request.StrategyId, request.Name,
            request.StartDate, request.EndDate, request.InitialCapital), cancellationToken);

        return Accepted(new { id, message = "Backtest queued. Poll GET /api/v1/backtests/{id} for results." });
    }

    /// <summary>GET /api/v1/backtests — List all backtests for current user</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var list = await _mediator.Send(new GetBacktestsQuery(UserId), cancellationToken);
        return Ok(list.Select(b => new
        {
            b.Id, b.Name, b.StrategyId, b.StartDate, b.EndDate,
            b.InitialCapital, Status = b.Status.ToString(), b.CreatedAt,
            hasResult = b.Result != null
        }));
    }

    /// <summary>GET /api/v1/backtests/{id} — Full backtest result</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var backtest = await _mediator.Send(new GetBacktestQuery(id, UserId), cancellationToken);
        if (backtest == null) return NotFound();
        return Ok(backtest);
    }

    /// <summary>GET /api/v1/backtests/{id}/equity-curve — Equity curve points only</summary>
    [HttpGet("{id}/equity-curve")]
    public async Task<IActionResult> GetEquityCurve(string id, CancellationToken cancellationToken)
    {
        var backtest = await _mediator.Send(new GetBacktestQuery(id, UserId), cancellationToken);
        if (backtest == null) return NotFound();
        if (backtest.Result == null) return Ok(new { status = backtest.Status.ToString(), equityCurve = Array.Empty<object>() });
        return Ok(new { status = backtest.Status.ToString(), equityCurve = backtest.Result.EquityCurve });
    }

    /// <summary>GET /api/v1/backtests/{id}/trades — Simulated trades only</summary>
    [HttpGet("{id}/trades")]
    public async Task<IActionResult> GetTrades(string id, CancellationToken cancellationToken)
    {
        var backtest = await _mediator.Send(new GetBacktestQuery(id, UserId), cancellationToken);
        if (backtest == null) return NotFound();
        return Ok(backtest.SimulatedTrades);
    }
}

public record RunBacktestRequest(string StrategyId, string Name, DateTime StartDate, DateTime EndDate, decimal InitialCapital);
