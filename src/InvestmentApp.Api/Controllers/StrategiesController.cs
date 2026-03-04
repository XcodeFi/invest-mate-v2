using InvestmentApp.Application.Strategies.Commands.CreateStrategy;
using InvestmentApp.Application.Strategies.Commands.UpdateStrategy;
using InvestmentApp.Application.Strategies.Commands.DeleteStrategy;
using InvestmentApp.Application.Strategies.Commands.LinkTrade;
using InvestmentApp.Application.Strategies.Queries.GetStrategies;
using InvestmentApp.Application.Strategies.Queries.GetStrategyPerformance;
using InvestmentApp.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/strategies")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class StrategiesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStrategyPerformanceService _performanceService;

    public StrategiesController(IMediator mediator, IStrategyPerformanceService performanceService)
    {
        _mediator = mediator;
        _performanceService = performanceService;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// List all strategies for current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StrategyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategies()
    {
        var query = new GetStrategiesQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get strategy detail by id
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategy(string id)
    {
        var query = new GetStrategiesQuery { UserId = GetUserId() };
        var strategies = await _mediator.Send(query);
        var strategy = strategies.FirstOrDefault(s => s.Id == id);
        if (strategy == null) return NotFound(new { message = "Strategy not found" });
        return Ok(strategy);
    }

    /// <summary>
    /// Create a new strategy
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateStrategy([FromBody] CreateStrategyCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetStrategy), new { id }, new { id });
    }

    /// <summary>
    /// Update an existing strategy
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateStrategy(string id, [FromBody] UpdateStrategyCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a strategy (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStrategy(string id)
    {
        var command = new DeleteStrategyCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Link a trade to a strategy
    /// </summary>
    [HttpPost("{id}/trades/{tradeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkTrade(string id, string tradeId)
    {
        var command = new LinkTradeToStrategyCommand
        {
            StrategyId = id,
            TradeId = tradeId,
            UserId = GetUserId()
        };
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Get strategy performance metrics
    /// </summary>
    [HttpGet("{id}/performance")]
    [ProducesResponseType(typeof(StrategyPerformanceSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformance(string id)
    {
        var query = new GetStrategyPerformanceQuery
        {
            StrategyId = id,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Compare multiple strategies
    /// </summary>
    [HttpPost("compare")]
    [ProducesResponseType(typeof(IEnumerable<StrategyComparisonItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompareStrategies([FromBody] CompareStrategiesRequest request)
    {
        var userId = GetUserId();
        var result = await _performanceService.CompareStrategiesAsync(request.StrategyIds, userId);
        return Ok(result);
    }
}

public class CompareStrategiesRequest
{
    public List<string> StrategyIds { get; set; } = new();
}
