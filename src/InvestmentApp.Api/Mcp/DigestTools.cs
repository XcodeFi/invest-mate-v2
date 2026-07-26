using System.ComponentModel;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class DigestTools
{
    [McpServerTool(Name = "get_daily_digest", ReadOnly = true)]
    [Description("Bản tin hằng ngày cho AI advisor: bối cảnh danh mục, số dư tiền mặt, gợi ý sizing — cùng payload (systemPrompt + userMessage) với POST /api/v1/ai/daily-digest.")]
    public static async Task<AiContextResult> GetDailyDigest(
        IAiAssistantService aiAssistant, IHttpContextAccessor http, CancellationToken ct)
    {
        var result = await aiAssistant.BuildDailyDigestAsync(http.GetUserId(), ct);
        if (result.ErrorMessage != null)
            throw new McpException(result.ErrorMessage);
        return result;
    }
}
