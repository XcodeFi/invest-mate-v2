using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Commands.DeleteTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Application.TradePlans.Commands.TriggerScenarioNode;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/trade-plans")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TradePlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public TradePlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// List all trade plans for current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TradePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTradePlans([FromQuery] bool activeOnly = false)
    {
        var query = new GetTradePlansQuery { UserId = GetUserId(), ActiveOnly = activeOnly };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get scenario preset templates
    /// </summary>
    [HttpGet("scenario-templates")]
    [ProducesResponseType(typeof(List<ScenarioPresetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScenarioTemplates()
    {
        var result = await _mediator.Send(new GetScenarioTemplatesQuery());
        return Ok(result);
    }

    /// <summary>
    /// Get a trade plan by id
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TradePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTradePlan(string id)
    {
        var query = new GetTradePlanByIdQuery { Id = id, UserId = GetUserId() };
        var result = await _mediator.Send(query);
        if (result == null) return NotFound(new { message = "Trade plan not found" });
        return Ok(result);
    }

    /// <summary>
    /// Create a new trade plan
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTradePlan([FromBody] CreateTradePlanCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTradePlan), new { id }, new { id });
    }

    /// <summary>
    /// Update trade plan fields
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateTradePlan(string id, [FromBody] UpdateTradePlanCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Update trade plan status (Ready, Executed, Cancelled, Reviewed)
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateTradePlanStatus(string id, [FromBody] UpdateTradePlanStatusCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Execute a specific lot in a multi-lot plan
    /// </summary>
    [HttpPatch("{id}/lots/{lotNumber:int}/execute")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExecuteLot(string id, int lotNumber, [FromBody] ExecuteLotCommand command)
    {
        command.PlanId = id;
        command.LotNumber = lotNumber;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Update stop-loss with history tracking
    /// </summary>
    [HttpPatch("{id}/stop-loss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateStopLoss(string id, [FromBody] UpdateStopLossCommand command)
    {
        command.PlanId = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Trigger an exit target (take profit, cut loss, etc.)
    /// </summary>
    [HttpPatch("{id}/exit-targets/{level:int}/trigger")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TriggerExitTarget(string id, int level, [FromBody] TriggerExitTargetCommand command)
    {
        command.PlanId = id;
        command.Level = level;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Get scenario history for a trade plan
    /// </summary>
    [HttpGet("{id}/scenario-history")]
    [ProducesResponseType(typeof(List<ScenarioHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScenarioHistory(string id)
    {
        try
        {
            var query = new GetScenarioHistoryQuery { TradePlanId = id, UserId = GetUserId() };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Trade plan not found" });
        }
    }

    /// <summary>
    /// Trigger a scenario node (manual or auto)
    /// </summary>
    [HttpPatch("{id}/scenario-nodes/{nodeId}/trigger")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TriggerScenarioNode(string id, string nodeId,
        [FromBody] TriggerScenarioNodeCommand command)
    {
        command.PlanId = id;
        command.NodeId = nodeId;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a trade plan (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTradePlan(string id)
    {
        var command = new DeleteTradePlanCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }
}
