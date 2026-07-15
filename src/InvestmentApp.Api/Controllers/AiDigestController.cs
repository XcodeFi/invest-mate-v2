using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Endpoint opt-in xác thực bằng ApiKey scheme (header X-Api-Key) — tách riêng khỏi
/// <see cref="AiController"/> (JWT-only) vì hai attribute [Authorize] khác scheme sẽ cộng dồn (AND),
/// không thể yêu cầu vừa JWT vừa ApiKey trên cùng action. Dùng cho NPU assistant kéo bản tin
/// hằng ngày theo cron rồi đẩy vào Claude phân tích timing.
/// </summary>
[ApiController]
[Route("api/v1/ai")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiDigestController : ControllerBase
{
    private readonly IAiAssistantService _aiAssistant;

    public AiDigestController(IAiAssistantService aiAssistant)
    {
        _aiAssistant = aiAssistant;
    }

    [HttpPost("daily-digest")]
    public async Task<IActionResult> DailyDigest()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _aiAssistant.BuildDailyDigestAsync(userId, HttpContext.RequestAborted);

        if (result.ErrorMessage != null)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result);
    }
}
