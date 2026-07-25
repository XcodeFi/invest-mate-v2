using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class SymbolToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetSymbolTimeline_SetsUserId_Symbol_Range()
    {
        McpTestContext.Capture<SymbolTimelineDto, GetSymbolTimelineQuery>(_mediator, out var sent, new SymbolTimelineDto());
        var from = new DateTime(2026, 1, 1);
        await SymbolTools.GetSymbolTimeline("VNM", from, null, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.From.Should().Be(from);
    }
}
