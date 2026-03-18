using InvestmentApp.Application.DailyRoutines.Commands.CompleteRoutineItem;
using InvestmentApp.Application.DailyRoutines.Commands.CreateCustomTemplate;
using InvestmentApp.Application.DailyRoutines.Commands.DeleteCustomTemplate;
using InvestmentApp.Application.DailyRoutines.Commands.GetOrCreateTodayRoutine;
using InvestmentApp.Application.DailyRoutines.Commands.SwitchTemplate;
using InvestmentApp.Application.DailyRoutines.Commands.UpdateCustomTemplate;
using InvestmentApp.Application.DailyRoutines.Queries.GetRoutineHistory;
using InvestmentApp.Application.DailyRoutines.Queries.GetRoutineTemplates;
using InvestmentApp.Application.DailyRoutines.Queries.GetSuggestedTemplate;
using InvestmentApp.Application.DailyRoutines.Queries.GetTodayRoutine;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/daily-routines")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DailyRoutinesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DailyRoutinesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get today's routine (null if not created yet)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTodayRoutine([FromQuery] string? localDate)
    {
        var query = new GetTodayRoutineQuery
        {
            UserId = GetUserId(),
            LocalDate = localDate
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get-or-create today's routine from template
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GetOrCreateTodayRoutine([FromBody] GetOrCreateTodayRoutineCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Toggle item completion
    /// </summary>
    [HttpPatch("{id}/items/{index:int}")]
    public async Task<IActionResult> CompleteRoutineItem(string id, int index,
        [FromBody] CompleteRoutineItemCommand command)
    {
        command.Id = id;
        command.ItemIndex = index;
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Switch today's template (discard current, create new)
    /// </summary>
    [HttpPost("switch-template")]
    public async Task<IActionResult> SwitchTemplate([FromBody] SwitchTemplateCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Get completion history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int days = 30)
    {
        var query = new GetRoutineHistoryQuery
        {
            UserId = GetUserId(),
            Days = days
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// List all templates (built-in + custom)
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var query = new GetRoutineTemplatesQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get auto-suggested template
    /// </summary>
    [HttpGet("templates/suggest")]
    public async Task<IActionResult> GetSuggestedTemplate([FromQuery] string? localDate)
    {
        var query = new GetSuggestedTemplateQuery
        {
            UserId = GetUserId(),
            LocalDate = localDate
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a custom template
    /// </summary>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateCustomTemplate([FromBody] CreateCustomTemplateCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Created($"/api/v1/daily-routines/templates/{id}", new { id });
    }

    /// <summary>
    /// Update a custom template
    /// </summary>
    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateCustomTemplate(string id,
        [FromBody] UpdateCustomTemplateCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a custom template (soft delete)
    /// </summary>
    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteCustomTemplate(string id)
    {
        var command = new DeleteCustomTemplateCommand { Id = id, UserId = GetUserId() };
        await _mediator.Send(command);
        return NoContent();
    }
}
