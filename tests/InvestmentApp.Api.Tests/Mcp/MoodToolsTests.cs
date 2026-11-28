using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetMoodHistory;
using InvestmentApp.Domain.Entities;
using MediatR;
using ModelContextProtocol;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class MoodToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetMoodHistory_dispatches_with_the_callers_id_and_the_range()
    {
        McpTestContext.Capture<MoodHistoryDto, GetMoodHistoryQuery>(_mediator, out var sent, new MoodHistoryDto { From = "a", To = "b" });
        var from = new DateTime(2026, 11, 1);
        var to = new DateTime(2026, 11, 28);

        await MoodTools.GetMoodHistory(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None, from, to);

        sent()!.UserId.Should().Be("u-1");
        sent()!.From.Should().Be(from);
        sent()!.To.Should().Be(to);
    }

    [Fact]
    public async Task GetMoodHistory_range_is_optional()
    {
        McpTestContext.Capture<MoodHistoryDto, GetMoodHistoryQuery>(_mediator, out var sent, new MoodHistoryDto());

        await MoodTools.GetMoodHistory(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        sent()!.From.Should().BeNull();
        sent()!.To.Should().BeNull();
    }

    [Theory]
    [InlineData("Calm", MoodState.Calm)]
    [InlineData("fomo", MoodState.Fomo)]
    [InlineData("FEAR", MoodState.Fear)]
    [InlineData(" Revenge ", MoodState.Revenge)]
    public async Task SetTodayMood_parses_case_insensitively_and_records_the_mcp_source(string input, MoodState expected)
    {
        McpTestContext.Capture<Unit, SetMoodCommand>(_mediator, out var sent, Unit.Value);

        var message = await MoodTools.SetTodayMood(input, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-1");
        sent()!.Mood.Should().Be(expected);
        sent()!.Source.Should().Be(MoodChange.SourceMcp);
        message.Should().Contain("agent");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Happy")]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("Fomo,Fear")]
    [InlineData("Calm, Fomo")]
    [InlineData("Fomo|Fear")]
    [InlineData("1,2")]
    [InlineData("Calm,Fear")]
    [InlineData(" fomo , revenge ")]
    [InlineData("Fomo Fear")]
    [InlineData("3")]
    public async Task SetTodayMood_rejects_an_invalid_value_with_a_vietnamese_error_and_writes_nothing(string input)
    {
        var act = () => MoodTools.SetTodayMood(input, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("Tâm trạng không hợp lệ");
        _mediator.Verify(m => m.Send(It.IsAny<SetMoodCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void The_tool_description_tells_agents_to_record_only_what_the_user_says_and_never_guess()
    {
        var description = typeof(MoodTools).GetMethod(nameof(MoodTools.SetTodayMood))!
            .GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().Single().Description;

        description.Should().Contain("NGƯỜI DÙNG").And.Contain("không tự đoán").And.Contain("KHÔNG mở khoá").And.Contain("đã bấm qua lớp phủ");
    }

    [Fact]
    public void The_history_dto_serializes_in_camelCase_with_per_change_sources()
    {
        var dto = new MoodHistoryDto
        {
            From = "2026-11-01", To = "2026-11-28",
            Days =
            {
                new MoodDayDto
                {
                    DateKey = "2026-11-27", Mood = MoodState.Fomo, Overrode = true, TradeCount = 2, EmotionalTrade = true,
                    Changes = { new MoodChangeDto { At = new DateTime(2026, 11, 27, 3, 0, 0, DateTimeKind.Utc), Mood = MoodState.Fomo, Source = "mcp" } }
                }
            }
        };

        var day = JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions).GetProperty("days")[0];

        day.GetProperty("dateKey").GetString().Should().Be("2026-11-27");
        day.GetProperty("emotionalTrade").GetBoolean().Should().BeTrue();
        day.GetProperty("tradeCount").GetInt32().Should().Be(2);
        day.GetProperty("changes")[0].GetProperty("source").GetString().Should().Be("mcp");
    }
}
