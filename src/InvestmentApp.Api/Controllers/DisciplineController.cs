using InvestmentApp.Application.Discipline.Queries;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DisciplineController : ControllerBase
{
    private readonly IMediator _mediator;

    public DisciplineController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Discipline Score cho widget Dashboard (§D6 plan Vin-discipline).
    /// Composite 0-100 (SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%)
    /// + 1 primitive Stop-Honor Rate + sample size.
    /// Cache 5 phút server-side.
    /// </summary>
    [HttpGet("discipline-score")]
    [ProducesResponseType(typeof(DisciplineScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDisciplineScore([FromQuery] int days = 90)
    {
        if (days <= 0) return BadRequest(new { error = "days must be positive" });
        if (days > 3650) return BadRequest(new { error = "days must be ≤ 3650" });

        var query = new GetDisciplineScoreQuery { UserId = GetUserId(), Days = days };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// List plans Ready/InProgress cần review thesis (V2.1 Vin-discipline §D5).
    /// Trigger: InvalidationRule.CheckDate ≤ today+2 HOẶC ExpectedReviewDate ≤ today.
    /// Sort theo DaysOverdue DESC (urgent nhất lên đầu).
    /// </summary>
    [HttpGet("thesis-reviews/pending")]
    [ProducesResponseType(typeof(List<PendingThesisReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingThesisReviews()
    {
        var query = new GetPendingThesisReviewsQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
