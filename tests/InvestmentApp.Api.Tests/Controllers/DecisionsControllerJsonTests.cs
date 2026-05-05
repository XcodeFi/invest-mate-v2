using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Configuration;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Verify JSON deserialization of ResolveDecisionRequest matches what the FE actually sends.
///
/// **Bug context:** Before fix, `JsonStringEnumConverter` was NOT registered globally — FE
/// sending <c>"Action": "HoldWithJournal"</c> (string) failed to deserialize → controller
/// binding produced null <c>request</c> → <c>request.Action</c> threw NullReferenceException
/// → 500 with detail "Object reference not set to an instance of an object."
///
/// These tests use <see cref="ApiJsonConfig.Configure"/> — the SAME factory Program.cs uses
/// via <c>AddJsonOptions</c>. If someone removes the converter from <see cref="ApiJsonConfig"/>,
/// these tests will fail (catches drift between Program.cs and tests).
/// </summary>
public class DecisionsControllerJsonTests
{
    private static JsonSerializerOptions BuildApiJsonOptions()
    {
        // Match the controller's binder: case-insensitive properties (ASP.NET default)
        // + the API-specific converters from ApiJsonConfig.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ApiJsonConfig.Configure(options);
        return options;
    }

    private static readonly JsonSerializerOptions ApiOptions = BuildApiJsonOptions();

    [Fact]
    public void ResolveDecisionRequest_DeserializesActionFromString_HoldWithJournal()
    {
        // Exact body shape FE sends per `decision.service.ts`.
        var json = """
        {
          "Action": "HoldWithJournal",
          "TradePlanId": null,
          "Symbol": "FPT",
          "Note": "FPT vẫn trong xu hướng tăng dài hạn, kết quả kinh doanh Q1 tốt."
        }
        """;

        var request = JsonSerializer.Deserialize<ResolveDecisionRequest>(json, ApiOptions);

        request.Should().NotBeNull();
        request!.Action.Should().Be(DecisionAction.HoldWithJournal);
        request.TradePlanId.Should().BeNull();
        request.Symbol.Should().Be("FPT");
        request.Note.Should().StartWith("FPT vẫn trong");
    }

    [Fact]
    public void ResolveDecisionRequest_DeserializesActionFromString_ExecuteSell()
    {
        var json = """{ "Action": "ExecuteSell", "TradePlanId": "plan-1" }""";

        var request = JsonSerializer.Deserialize<ResolveDecisionRequest>(json, ApiOptions);

        request.Should().NotBeNull();
        request!.Action.Should().Be(DecisionAction.ExecuteSell);
        request.TradePlanId.Should().Be("plan-1");
    }

    [Fact]
    public void ResolveDecisionRequest_AlsoAcceptsActionAsInteger_BackwardsCompat()
    {
        // Default JsonStringEnumConverter accepts both string and integer (allowIntegerValues=true default).
        var json = """{ "Action": 1, "Symbol": "FPT", "Note": "abcdefghijklmnopqrst" }""";

        var request = JsonSerializer.Deserialize<ResolveDecisionRequest>(json, ApiOptions);

        request.Should().NotBeNull();
        request!.Action.Should().Be(DecisionAction.HoldWithJournal);  // 1 = second value
    }

    [Fact]
    public void DecisionItemDto_SerializesEnumsAsStrings_FrontEndExpectsLiteral()
    {
        // FE typescript declares: `type DecisionType = 'StopLossHit' | 'ScenarioTrigger' | 'ThesisReviewDue'`.
        // BE must serialize enums as strings, not integers, otherwise FE comparison `s === 'Critical'`
        // silently fails and severity badge falls through to default label.
        var dto = new InvestmentApp.Application.Decisions.DTOs.DecisionItemDto
        {
            Id = "StopLossHit:p1:FPT",
            Type = InvestmentApp.Application.Decisions.DTOs.DecisionType.StopLossHit,
            Severity = InvestmentApp.Application.Decisions.DTOs.DecisionSeverity.Critical,
            Symbol = "FPT",
            Headline = "x",
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(dto, ApiOptions);

        json.Should().Contain("\"StopLossHit\"");
        json.Should().Contain("\"Critical\"");
        json.Should().NotContain("\"Type\":0");      // not int
        json.Should().NotContain("\"Severity\":0");
    }
}
