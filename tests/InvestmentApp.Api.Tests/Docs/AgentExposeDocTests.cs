using FluentAssertions;
using InvestmentApp.Api.Controllers;

namespace InvestmentApp.Api.Tests.Docs;

/// <summary>
/// Guard: tài liệu agent phải liệt kê đủ 5 nhóm endpoint mới (positions, watchlists, journal-entries,
/// journals, symbol timeline). Thêm controller mà quên cập nhật doc → test đỏ.
/// </summary>
public class AgentExposeDocTests
{
    [Theory]
    [InlineData("positions")]
    [InlineData("watchlists")]
    [InlineData("journal-entries")]
    [InlineData("journals")]
    [InlineData("symbol-timeline")]
    public void Doc_ContainsSectionAnchor(string anchor)
    {
        var doc = AiAgentController.LoadDoc();
        doc.Should().Contain($"id=\"{anchor}\"",
            $"tài liệu AI-Agent-TradePlan-API.md phải có mục '{anchor}' (vừa thêm controller? cập nhật doc)");
    }

    [Theory]
    [InlineData("GET /api/v1/ai/agent/positions")]
    [InlineData("GET /api/v1/ai/agent/watchlists")]
    [InlineData("POST /api/v1/ai/agent/watchlists")]
    [InlineData("POST /api/v1/ai/agent/journal-entries")]
    [InlineData("GET /api/v1/ai/agent/journals")]
    [InlineData("GET /api/v1/ai/agent/symbols/{symbol}/timeline")]
    public void Doc_ContainsRoute(string route)
    {
        var doc = AiAgentController.LoadDoc();
        doc.Should().Contain(route);
    }
}
