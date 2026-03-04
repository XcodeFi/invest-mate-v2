using InvestmentApp.Application.Risk.Commands.SetRiskProfile;
using InvestmentApp.Application.Risk.Commands.SetStopLossTarget;
using InvestmentApp.Application.Risk.Queries.GetRiskProfile;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetDrawdown;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetCorrelation;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/risk")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RiskController : ControllerBase
{
    private readonly IMediator _mediator;

    public RiskController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get risk profile for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/profile")]
    [ProducesResponseType(typeof(RiskProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRiskProfile(string portfolioId)
    {
        var query = new GetRiskProfileQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        if (result == null)
            return NotFound(new { message = "No risk profile found. Create one to set risk limits." });
        return Ok(result);
    }

    /// <summary>
    /// Set or update risk profile for a portfolio
    /// </summary>
    [HttpPost("portfolio/{portfolioId}/profile")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRiskProfile(string portfolioId, [FromBody] SetRiskProfileCommand command)
    {
        command.PortfolioId = portfolioId;
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Get portfolio risk summary (positions, drawdown, VaR)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/summary")]
    [ProducesResponseType(typeof(PortfolioRiskSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolioRisk(string portfolioId)
    {
        var query = new GetPortfolioRiskQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get max drawdown analysis
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/drawdown")]
    [ProducesResponseType(typeof(DrawdownResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDrawdown(string portfolioId)
    {
        var query = new GetDrawdownQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get correlation matrix for portfolio symbols
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/correlation")]
    [ProducesResponseType(typeof(CorrelationMatrix), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCorrelation(string portfolioId)
    {
        var query = new GetCorrelationQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get stop-loss/target settings for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/stop-loss")]
    [ProducesResponseType(typeof(StopLossTargetsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStopLossTargets(string portfolioId)
    {
        var query = new GetStopLossTargetsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Set stop-loss and target for a trade
    /// </summary>
    [HttpPost("stop-loss")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStopLossTarget([FromBody] SetStopLossTargetCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }
}
