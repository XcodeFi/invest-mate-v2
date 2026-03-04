using InvestmentApp.Application.Alerts.Commands.CreateAlertRule;
using InvestmentApp.Application.Alerts.Commands.UpdateAlertRule;
using InvestmentApp.Application.Alerts.Commands.DeleteAlertRule;
using InvestmentApp.Application.Alerts.Commands.MarkAlertRead;
using InvestmentApp.Application.Alerts.Queries.GetAlertRules;
using InvestmentApp.Application.Alerts.Queries.GetAlertHistory;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AlertsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// List all alert rules for user
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(IEnumerable<AlertRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlertRules()
    {
        var query = new GetAlertRulesQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a new alert rule
    /// </summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAlertRule([FromBody] CreateAlertRuleCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/alerts/rules/{id}", new { id });
    }

    /// <summary>
    /// Update an alert rule
    /// </summary>
    [HttpPut("rules/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateAlertRule(string id, [FromBody] UpdateAlertRuleCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete an alert rule
    /// </summary>
    [HttpDelete("rules/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAlertRule(string id)
    {
        var command = new DeleteAlertRuleCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Get alert history
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(AlertHistoryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlertHistory([FromQuery] bool unreadOnly = false)
    {
        var query = new GetAlertHistoryQuery
        {
            UserId = GetUserId(),
            UnreadOnly = unreadOnly
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Mark alert as read
    /// </summary>
    [HttpPut("{id}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var command = new MarkAlertReadCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }
}
