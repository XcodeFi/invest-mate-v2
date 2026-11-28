using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetSignalSetups;
using ModelContextProtocol;
using Moq;
using MediatR;

namespace InvestmentApp.Api.Tests.Mcp;

public class SignalSetupsToolTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task get_signal_setups_sends_the_normalized_symbol()
    {
        McpTestContext.Capture<SignalSetupsDto, GetSignalSetupsQuery>(_mediator, out var sent, new SignalSetupsDto { Symbol = "FPT" });

        await MarketDataTools.GetSignalSetups("  fpt ", _mediator.Object, CancellationToken.None);

        sent()!.Symbol.Should().Be("FPT");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task blank_symbol_is_refused_with_a_clear_message_and_nothing_is_sent(string bad)
    {
        var act = async () => await MarketDataTools.GetSignalSetups(bad, _mediator.Object, CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("symbol");
        _mediator.Verify(m => m.Send(It.IsAny<GetSignalSetupsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void serialization_below_30_trades_omits_winrate_and_ev_rather_than_writing_zero()
    {
        var dto = new SignalSetupsDto
        {
            Symbol = "FPT", Adjusted = true,
            Setups =
            {
                new SignalSetupDto
                {
                    Key = "pullback", Name = "Pullback xu hướng", Description = "d", Signals = 11, Completed = 7, Open = 1, Skipped = 2,
                    N = 7, SampleSufficient = false
                }
            },
            Notes = { "n1" }
        };

        var json = JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions);
        var setup = json.GetProperty("setups")[0];

        setup.GetProperty("n").GetInt32().Should().Be(7);
        setup.GetProperty("minSample").GetInt32().Should().Be(30);
        setup.GetProperty("sampleSufficient").GetBoolean().Should().BeFalse();
        foreach (var name in new[] { "winRate", "winRateLow", "winRateHigh", "evR", "evRLow", "evRHigh", "avgWinR", "avgLossR", "activePlan", "activeSince" })
            if (setup.TryGetProperty(name, out var v))
                v.ValueKind.Should().Be(JsonValueKind.Null, $"{name}: chưa đủ mẫu thì vắng mặt/null, không bao giờ là 0");
        json.GetProperty("disclaimer").GetString().Should().Be("Kết quả quá khứ, không đảm bảo tương lai");
        json.GetProperty("notes")[0].GetString().Should().Be("n1");
        json.TryGetProperty("reasonUnavailable", out var reason).Should().BeFalse("không có lý do thì vắng mặt");
        _ = reason;
    }

    [Fact]
    public void serialization_carries_the_active_plan_and_the_stats_when_present()
    {
        var dto = new SignalSetupsDto
        {
            Symbol = "FPT",
            Setups =
            {
                new SignalSetupDto
                {
                    Key = "pullback", Name = "n", Description = "d", N = 31, SampleSufficient = true,
                    WinRate = 0.52m, WinRateLow = 0.35m, WinRateHigh = 0.68m, EvR = 0.2m, EvRLow = -0.1m, EvRHigh = 0.5m,
                    ActiveSince = new DateTime(2026, 11, 28),
                    ActivePlan = new SignalActivePlanDto
                    {
                        SignalDate = new DateTime(2026, 11, 28), Entry = 100_000m, EntryIsEstimate = true, Stop = 96_000m, Target = 108_000m, Status = "moi"
                    }
                }
            }
        };

        var setup = JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions).GetProperty("setups")[0];

        setup.GetProperty("winRate").GetDecimal().Should().Be(0.52m);
        setup.GetProperty("evRLow").GetDecimal().Should().Be(-0.1m, "số âm vẫn được giữ");
        var plan = setup.GetProperty("activePlan");
        plan.GetProperty("entryIsEstimate").GetBoolean().Should().BeTrue();
        plan.GetProperty("status").GetString().Should().Be("moi");
        plan.TryGetProperty("dayInTrade", out _).Should().BeFalse("chưa vào lệnh thì không có ngày giữ");
    }

    [Fact]
    public void serialization_of_an_unavailable_result_has_the_reason_and_no_setups()
    {
        var dto = new SignalSetupsDto { Symbol = "NEW", ReasonUnavailable = "Chưa đủ lịch sử giá đã điều chỉnh để thống kê" };

        var json = JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions);

        json.GetProperty("reasonUnavailable").GetString().Should().Contain("Chưa đủ lịch sử");
        json.GetProperty("setups").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void the_mcp_payload_has_no_per_trade_list()
    {
        var names = JsonSerializer.SerializeToElement(new SignalSetupsDto { Setups = { new SignalSetupDto { Key = "pullback", Name = "n", Description = "d" } } },
            McpJsonUtilities.DefaultOptions).GetRawText();

        names.ToLowerInvariant().Should().NotContain("trades").And.NotContain("outcome");
    }
}
