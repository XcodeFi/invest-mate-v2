using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol;
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
        await SymbolTools.GetSymbolTimeline("VNM", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None, from: from);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.From.Should().Be(from);
    }

    [Fact]
    public void GetSymbolTimeline_DescriptionDocumentsTheDossierAtTradeBadge()
    {
        var description = typeof(SymbolTools).GetMethod(nameof(SymbolTools.GetSymbolTimeline))!
            .GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().Single().Description;

        description.Should().Contain("dossierAtTrade").And.Contain("resigned-after").And.Contain("needs-review");
    }

    [Fact]
    public void TimelineTradeItem_SerializesDossierAtTradeInCamelCase()
    {
        var dto = new SymbolTimelineDto
        {
            Items =
            {
                new TimelineItemDto
                {
                    Type = "trade", Timestamp = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                    Data = new { Id = "t1", DossierAtTrade = new InvestmentApp.Domain.Entities.DossierAtTrade("signed", 95, null, "needs-review") }
                }
            }
        };

        var item = JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions).GetProperty("items")[0].GetProperty("data");

        var badge = item.GetProperty("dossierAtTrade");
        badge.GetProperty("state").GetString().Should().Be("signed");
        badge.GetProperty("daysSigned").GetInt32().Should().Be(95);
        badge.GetProperty("freshness").GetString().Should().Be("needs-review");
    }
}
