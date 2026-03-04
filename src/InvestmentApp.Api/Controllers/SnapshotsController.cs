using InvestmentApp.Application.Snapshots.Commands.TakeSnapshot;
using InvestmentApp.Application.Snapshots.Queries.GetSnapshotAtDate;
using InvestmentApp.Application.Snapshots.Queries.GetSnapshotRange;
using InvestmentApp.Application.Snapshots.Queries.CompareSnapshots;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/snapshots")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SnapshotsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SnapshotsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get portfolio snapshot at a specific date
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/at/{date}")]
    [ProducesResponseType(typeof(SnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshotAtDate(string portfolioId, DateTime date)
    {
        var query = new GetSnapshotAtDateQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            Date = date
        };
        var result = await _mediator.Send(query);
        if (result == null)
            return NotFound(new { message = $"No snapshot found for date {date:yyyy-MM-dd}" });
        return Ok(result);
    }

    /// <summary>
    /// Get snapshot range for timeline view
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/range")]
    [ProducesResponseType(typeof(List<SnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshotRange(
        string portfolioId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = new GetSnapshotRangeQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            From = from ?? DateTime.UtcNow.AddMonths(-3),
            To = to ?? DateTime.UtcNow
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Compare snapshots at two different dates
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/compare")]
    [ProducesResponseType(typeof(SnapshotComparisonDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompareSnapshots(
        string portfolioId,
        [FromQuery] DateTime date1,
        [FromQuery] DateTime date2)
    {
        var query = new CompareSnapshotsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            Date1 = date1,
            Date2 = date2
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Manually take a snapshot of a portfolio
    /// </summary>
    [HttpPost("portfolio/{portfolioId}/take")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TakeSnapshot(string portfolioId)
    {
        var command = new TakeSnapshotCommand
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(command);
        if (!result)
            return NotFound();
        return Ok(new { message = "Snapshot taken successfully" });
    }

    /// <summary>
    /// Get timeline view (all snapshots for a portfolio)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/timeline")]
    [ProducesResponseType(typeof(List<SnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(string portfolioId)
    {
        var query = new GetSnapshotRangeQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            From = DateTime.MinValue,
            To = DateTime.UtcNow
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
