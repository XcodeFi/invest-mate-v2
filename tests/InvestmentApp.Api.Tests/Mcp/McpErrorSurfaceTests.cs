using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using InvestmentApp.Api.Mcp;
using ModelContextProtocol;
using Xunit;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// SDK che mọi exception của tool thành "An error occurred invoking '&lt;tên tool&gt;'." — chỉ
/// <see cref="McpException"/> đi xuyên qua được. Agent chỉ giao tiếp qua MCP nên message bị che
/// là mất hoàn toàn đường tự chữa.
/// </summary>
public class McpErrorSurfaceTests
{
    private static Func<Task<string>> Throwing(Exception ex)
        => () => McpErrorTranslator.RunAsync(() => Task.FromException<string>(ex));

    [Fact]
    public async Task ValidationException_Becomes_McpException_Keeping_Message()
    {
        var failure = new ValidationFailure("ScenarioNodes[0].ActionType",
            "actionType bắt buộc, một trong: SellPercent, SellAll");

        var ex = await Throwing(new ValidationException(new[] { failure }))
            .Should().ThrowAsync<McpException>();

        ex.And.Message.Should().Contain("ScenarioNodes[0].ActionType");
        ex.And.Message.Should().Contain("SellPercent");
    }

    [Fact]
    public async Task JsonException_Becomes_McpException_Keeping_Path()
    {
        var ex = await Throwing(new System.Text.Json.JsonException(
                "The JSON value could not be converted to ScenarioActionType. Path: $.scenarioNodes[0].actionType"))
            .Should().ThrowAsync<McpException>();

        ex.And.Message.Should().Contain("$.scenarioNodes[0].actionType");
    }

    [Fact]
    public async Task InvalidOperationException_Becomes_McpException_Keeping_Message()
    {
        var ex = await Throwing(new InvalidOperationException("Cannot set scenario nodes in Simple mode"))
            .Should().ThrowAsync<McpException>();

        ex.And.Message.Should().Contain("Simple mode");
    }

    [Fact]
    public async Task McpException_Passes_Through_Without_Rewrapping()
    {
        var ex = await Throwing(new McpException("Cổng hồ sơ công ty chặn mã ANV"))
            .Should().ThrowAsync<McpException>();

        ex.And.Message.Should().Be("Cổng hồ sơ công ty chặn mã ANV");
    }

    [Fact]
    public async Task Success_Passes_Through_Untouched()
    {
        var result = await McpErrorTranslator.RunAsync(() => Task.FromResult("ok"));
        result.Should().Be("ok");
    }
}
