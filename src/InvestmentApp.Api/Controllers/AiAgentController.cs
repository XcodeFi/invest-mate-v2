using System.Reflection;
using InvestmentApp.Api.Auth;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Bề mặt agent (scheme ApiKey) cho NPU/Claude ghi trade plan + trade — mở rộng ADR-0003 sang thao tác
/// ghi (xem ADR-0004). Re-dispatch các MediatR command sẵn có; adapter chèn guard (ép Draft, chặn
/// restore, gán UserId/Origin). Không chứa business logic. Tách controller riêng vì scheme khác nhau
/// trên [Authorize] cộng dồn (AND) — theo đúng precedent AiDigestController.
/// </summary>
[ApiController]
[Route("api/v1/ai/agent")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiAgentController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    // ---- Read ----

    [HttpGet("trade-plans")]
    public async Task<IActionResult> GetPlans([FromQuery] bool activeOnly = false)
        => Ok(await _mediator.Send(new GetTradePlansQuery { UserId = GetUserId(), ActiveOnly = activeOnly }));

    [HttpGet("trade-plans/{id}")]
    public async Task<IActionResult> GetPlan(string id)
    {
        var result = await _mediator.Send(new GetTradePlanByIdQuery { Id = id, UserId = GetUserId() });
        return result == null ? NotFound(new { message = "Trade plan not found" }) : Ok(result);
    }

    // ---- Write (re-dispatch + adapter guards) ----

    [HttpPost("trade-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateTradePlanCommand command)
    {
        command.UserId = GetUserId();
        command.Status = null;    // ép Draft — agent không one-shot execute (ADR-0004)
        command.TradeId = null;
        var id = await _mediator.Send(command);
        return Created($"/api/v1/ai/agent/trade-plans/{id}", new { id });
    }

    [HttpPut("trade-plans/{id}")]
    public async Task<IActionResult> UpdatePlan(string id, [FromBody] UpdateTradePlanCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("trade-plans/{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateTradePlanStatusCommand command)
    {
        if (string.Equals(command.Status, "restore", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "restore không được phép qua agent surface" });
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPost("trades")]
    public async Task<IActionResult> CreateTrade([FromBody] CreateTradeCommand command)
    {
        command.UserId = GetUserId();
        command.Origin = "AI_AGENT";
        var id = await _mediator.Send(command);
        return Created($"/api/v1/trades/{id}", new { id });
    }

    // ---- Reference doc (versioned, cache-friendly) ----

    /// <summary>Version của tài liệu = version API mỗi lần deploy (informational version của assembly).</summary>
    public static readonly string DocVersion =
        typeof(AiAgentController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AiAgentController).Assembly.GetName().Version?.ToString()
        ?? "0";

    /// <summary>Nội dung tài liệu nhúng — resolve resource theo suffix để tránh phụ thuộc cách MSBuild đặt tên.</summary>
    public static string LoadDoc()
    {
        var asm = typeof(AiAgentController).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("AI-Agent-TradePlan-API.md", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded doc AI-Agent-TradePlan-API.md not found");
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Trả tài liệu API (Claude trỏ tới như mục lục). ETag = docVersion để NPU cache local + conditional GET.
    /// </summary>
    [HttpGet("doc")]
    public IActionResult GetDoc()
    {
        var etag = $"\"{DocVersion}\"";
        if (Request.Headers.IfNoneMatch.Any(v => v == etag || v == "*"))
            return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;
        return new ContentResult { Content = LoadDoc(), ContentType = "text/markdown", StatusCode = 200 };
    }
}
