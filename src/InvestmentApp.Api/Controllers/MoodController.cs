using InvestmentApp.Application.Mood.Commands.MarkMoodOverride;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetTodayMood;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Tâm trạng tự chấm mỗi ngày, dùng cho màn tĩnh tâm trên trang chủ (ADR-0013).
/// Ngày lịch VN do server tính — không nhận ngày từ client.
/// </summary>
[ApiController]
[Route("api/v1/mood")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MoodController : ControllerBase
{
    private readonly IMediator _mediator;

    public MoodController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>Tâm trạng đã chấm hôm nay, hoặc <c>mood: null</c> nếu chưa chấm.</summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(TodayMoodDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetToday(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTodayMoodQuery { UserId = GetUserId() }, cancellationToken);
        return Ok(result);
    }

    /// <summary>Chấm tâm trạng cho hôm nay. Chấm lại trong ngày là ghi đè, không tạo bản ghi thứ hai.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetMood([FromBody] SetMoodCommand command, CancellationToken cancellationToken)
    {
        command.UserId = GetUserId();
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Đóng dấu đã bấm qua lớp phủ. 404 khi hôm nay chưa chấm tâm trạng.</summary>
    [HttpPost("override")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkOverride(CancellationToken cancellationToken)
    {
        var stamped = await _mediator.Send(new MarkMoodOverrideCommand { UserId = GetUserId() }, cancellationToken);
        return stamped ? NoContent() : NotFound();
    }
}
