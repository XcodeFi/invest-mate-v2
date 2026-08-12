using InvestmentApp.Application.ClientLogs.Commands.RecordClientLog;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Nhận lỗi chưa bắt được từ trình duyệt và đẩy vào cùng đường log với lỗi backend.
///
/// Có <c>[Authorize]</c> vì endpoint này chuyển tiếp nội dung ra một dịch vụ bên ngoài
/// (Telegram): để mở là ai cũng bơm được nội dung tuỳ ý vào hộp thư cảnh báo, và cách nhanh
/// nhất để một kênh cảnh báo trở nên vô dụng là biến nó thành chỗ bị làm phiền.
/// </summary>
[ApiController]
[Route("api/v1/client-logs")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ClientLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientLogsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Trả 200 rỗng kể cả khi không có gì để làm — client bắn-và-quên, không đọc body.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Record([FromBody] RecordClientLogCommand? command, CancellationToken ct)
    {
        // Dự án đặt `SuppressModelStateInvalidFilter = true`, nên body hỏng/rỗng KHÔNG tự thành
        // 400 — nó bind ra null. Không chặn ở đây là NullReferenceException, tức 500, tức một
        // request méo cũng bắn được tin nhắn vào kênh cảnh báo.
        if (command is null)
            return BadRequest(new { error = "Body không hợp lệ." });

        // Danh tính lấy từ JWT, không nhận từ body — nếu không thì client tự khai mình là ai.
        command.UserId = User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

        await _mediator.Send(command, ct);
        return Ok();
    }
}
