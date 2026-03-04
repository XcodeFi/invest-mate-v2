using InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;
using InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;
using InvestmentApp.Application.Portfolios.Commands.DeletePortfolio;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Queries.GetTradesByPortfolio;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/portfolios")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PortfoliosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PortfoliosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get all portfolios for the current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PortfolioSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPortfolios()
    {
        var userId = GetUserId();
        var query = new GetAllPortfoliosQuery { UserId = userId };
        var portfolios = await _mediator.Send(query);
        return Ok(portfolios);
    }

    /// <summary>
    /// Get a specific portfolio by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPortfolio(string id)
    {
        var userId = GetUserId();
        var query = new GetPortfolioQuery { Id = id, UserId = userId };
        var portfolio = await _mediator.Send(query);

        if (portfolio == null)
            return NotFound();

        return Ok(portfolio);
    }

    /// <summary>
    /// Create a new portfolio
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioCommand command)
    {
        command.UserId = GetUserId();
        var portfolioId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetPortfolio), new { id = portfolioId }, new { id = portfolioId });
    }

    /// <summary>
    /// Update an existing portfolio
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePortfolio(string id, [FromBody] UpdatePortfolioCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);

        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Delete a portfolio (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePortfolio(string id)
    {
        var userId = GetUserId();
        var command = new DeletePortfolioCommand { Id = id, UserId = userId };
        var result = await _mediator.Send(command);

        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get trades for a specific portfolio
    /// </summary>
    [HttpGet("{id}/trades")]
    [ProducesResponseType(typeof(TradeListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolioTrades(
        string id,
        [FromQuery] string? symbol,
        [FromQuery] string? tradeType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var query = new GetTradesByPortfolioQuery
        {
            PortfolioId = id,
            UserId = userId,
            Symbol = symbol,
            TradeType = tradeType,
            Page = page,
            PageSize = pageSize
        };
        var trades = await _mediator.Send(query);
        return Ok(trades);
    }
}