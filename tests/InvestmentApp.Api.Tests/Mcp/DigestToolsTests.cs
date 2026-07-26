using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using ModelContextProtocol;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class DigestToolsTests
{
    private readonly Mock<IAiAssistantService> _aiAssistant = new();

    [Fact]
    public async Task GetDailyDigest_PassesUserId_AndReturnsResult()
    {
        var expected = new AiContextResult { SystemPrompt = "sys", UserMessage = "msg" };
        _aiAssistant.Setup(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await DigestTools.GetDailyDigest(
            _aiAssistant.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        result.Should().BeSameAs(expected);
        _aiAssistant.Verify(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDailyDigest_ErrorMessage_ThrowsMcpException()
    {
        _aiAssistant.Setup(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiContextResult { ErrorMessage = "Chưa có danh mục nào." });

        var act = () => DigestTools.GetDailyDigest(
            _aiAssistant.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        await act.Should().ThrowAsync<McpException>().WithMessage("Chưa có danh mục nào.");
    }
}
