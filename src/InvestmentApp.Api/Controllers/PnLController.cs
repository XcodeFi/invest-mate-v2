using InvestmentApp.Application.Portfolios.Queries.GetPnL;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/pnl")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PnLController : ControllerBase
{
    private readonly IMediator _mediator;

    public PnLController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get P&L summary for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}")]
    [ProducesResponseType(typeof(PortfolioPnLDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPortfolioPnL(string portfolioId)
    {
        try
        {
            // Kiểm chủ sở hữu nằm trong handler (cùng đường với MCP); danh mục không có hoặc của người khác → 404 thân rỗng
            return Ok(await _mediator.Send(new GetPortfolioPnLQuery { PortfolioId = portfolioId, UserId = GetUserId() }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get P&L for a specific position in a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/position/{symbol}")]
    [ProducesResponseType(typeof(Application.Portfolios.Queries.PositionPnL), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPositionPnL(string portfolioId, string symbol)
    {
        try
        {
            return Ok(await _mediator.Send(new GetPositionPnLQuery
            {
                PortfolioId = portfolioId,
                Symbol = symbol,
                UserId = GetUserId()
            }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get P&L summary across all user portfolios
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(OverallPnLDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverallPnL()
        => Ok(await _mediator.Send(new GetOverallPnLQuery { UserId = GetUserId() }));
}
