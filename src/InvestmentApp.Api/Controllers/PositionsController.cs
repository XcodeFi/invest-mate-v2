using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/positions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PositionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PositionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get all active (open) positions across portfolios
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ActivePositionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePositions([FromQuery] string? portfolioId = null)
    {
        var query = new GetActivePositionsQuery
        {
            UserId = GetUserId(),
            PortfolioId = portfolioId
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
