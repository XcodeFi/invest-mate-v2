using Microsoft.AspNetCore.Http;

namespace InvestmentApp.Api.Mcp;

/// <summary>Resolve UserId cho MCP tool từ claim "sub" — cùng cơ chế AiAgentControllerBase.GetUserId().</summary>
public static class McpUserContext
{
    public static string GetUserId(this IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Thiếu claim 'sub' — API key không hợp lệ.");
}
