using InvestmentApp.Application.Analytics.Queries.GetPerformance;
using InvestmentApp.Application.Analytics.Queries.GetEquityCurve;
using InvestmentApp.Application.Analytics.Queries.GetMonthlyReturns;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AdvancedAnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdvancedAnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get full performance metrics (CAGR, Sharpe, Sortino, WinRate, ProfitFactor, Expectancy)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/performance")]
    [ProducesResponseType(typeof(PerformanceSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformance(string portfolioId)
    {
        var query = new GetPerformanceQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get equity curve data for charting
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/equity-curve")]
    [ProducesResponseType(typeof(EquityCurveData), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEquityCurve(string portfolioId)
    {
        var query = new GetEquityCurveQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly returns heatmap data
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/monthly-returns")]
    [ProducesResponseType(typeof(MonthlyReturnsData), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyReturns(string portfolioId)
    {
        var query = new GetMonthlyReturnsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
