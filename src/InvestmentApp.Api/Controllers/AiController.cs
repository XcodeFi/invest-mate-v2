using System.Text.Json;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AiController : ControllerBase
{
    private readonly IAiAssistantService _aiAssistant;

    public AiController(IAiAssistantService aiAssistant)
    {
        _aiAssistant = aiAssistant;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    public class JournalReviewRequest
    {
        public string? PortfolioId { get; set; }
        public string? Question { get; set; }
    }

    public class PortfolioReviewRequest
    {
        public string PortfolioId { get; set; } = null!;
        public string? Question { get; set; }
    }

    public class TradePlanAdvisorRequest
    {
        public string TradePlanId { get; set; } = null!;
        public string? Question { get; set; }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = null!;
        public List<AiChatMessage>? History { get; set; }
    }

    public class MonthlySummaryRequest
    {
        public string PortfolioId { get; set; } = null!;
        public int Year { get; set; }
        public int Month { get; set; }
    }

    [HttpPost("journal-review")]
    public async Task StreamJournalReview([FromBody] JournalReviewRequest request)
    {
        await StreamResponse(_aiAssistant.ReviewJournalAsync(
            GetUserId(), request.PortfolioId, request.Question, HttpContext.RequestAborted));
    }

    [HttpPost("portfolio-review")]
    public async Task StreamPortfolioReview([FromBody] PortfolioReviewRequest request)
    {
        await StreamResponse(_aiAssistant.ReviewPortfolioAsync(
            GetUserId(), request.PortfolioId, request.Question, HttpContext.RequestAborted));
    }

    [HttpPost("trade-plan-advisor")]
    public async Task StreamTradePlanAdvisor([FromBody] TradePlanAdvisorRequest request)
    {
        await StreamResponse(_aiAssistant.AdviseTradePlanAsync(
            GetUserId(), request.TradePlanId, request.Question, HttpContext.RequestAborted));
    }

    [HttpPost("chat")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        await StreamResponse(_aiAssistant.ChatAsync(
            GetUserId(), request.Message, request.History, HttpContext.RequestAborted));
    }

    [HttpPost("monthly-summary")]
    public async Task StreamMonthlySummary([FromBody] MonthlySummaryRequest request)
    {
        await StreamResponse(_aiAssistant.MonthlySummaryAsync(
            GetUserId(), request.PortfolioId, request.Year, request.Month, HttpContext.RequestAborted));
    }

    private async Task StreamResponse(IAsyncEnumerable<AiStreamChunk> stream)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        await foreach (var chunk in stream)
        {
            var json = JsonSerializer.Serialize(chunk, jsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        await Response.WriteAsync("data: [DONE]\n\n");
        await Response.Body.FlushAsync();
    }
}
