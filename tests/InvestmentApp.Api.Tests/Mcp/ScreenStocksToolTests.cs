using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.MarketData.Queries.ScreenStocks;
using InvestmentApp.Application.MarketData.Screener;
using MediatR;
using ModelContextProtocol;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class ScreenStocksToolTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task screen_stocks_with_no_arguments_sends_an_all_null_query()
    {
        McpTestContext.Capture<ScreenerResult, ScreenStocksQuery>(_mediator, out var sent, new ScreenerResult());

        await MarketDataTools.ScreenStocks(_mediator.Object, CancellationToken.None);

        var q = sent()!;
        q.Floors.Should().BeNull();
        q.MinAdtv.Should().BeNull();
        q.MaxPe.Should().BeNull("trần P/E mặc định TẮT");
        q.IncludeExcluded.Should().BeNull();
    }

    [Fact]
    public async Task screen_stocks_forwards_every_argument()
    {
        McpTestContext.Capture<ScreenerResult, ScreenStocksQuery>(_mediator, out var sent, new ScreenerResult());

        await MarketDataTools.ScreenStocks(_mediator.Object, CancellationToken.None,
            floors: "HOSE", includeUpcom: true, minAdtv: 10m, minPrice: 7000m, minCap: 2000m, minRoe: 15m, maxPe: 25m,
            minRs3m: 60, skipQualityLayer: true, includeExcluded: true, excludedSkip: 5, excludedTake: 20, lookupSymbol: "fpt");

        var q = sent()!;
        (q.Floors, q.IncludeUpcom, q.MinAdtv, q.MinPrice, q.MinCap, q.MinRoe, q.MaxPe, q.MinRs3m, q.SkipQualityLayer,
            q.IncludeExcluded, q.ExcludedSkip, q.ExcludedTake, q.LookupSymbol)
            .Should().Be(("HOSE", true, 10m, 7000m, 2000m, 15m, 25m, 60, true, true, 5, 20, "fpt"));
    }

    [Fact]
    public void serialization_carries_machine_readable_freshness_and_no_score_field()
    {
        var result = new ScreenerResult
        {
            Status = "stale", Stale = true, AsOf = new DateTimeOffset(2026, 11, 28, 3, 0, 0, TimeSpan.Zero),
            ExcludedCounts = { ["ROE thấp"] = 3 },
            Passed = { new ScreenerPassedRow { Symbol = "FPT", Floor = "HOSE", Flags = { "beta cao (2.1)" } } },
            UnverifiedHose = { new ScreenerPassedRow { Symbol = "VNM", Floor = "HOSE" } }
        };

        var json = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);

        json.GetProperty("status").GetString().Should().Be("stale");
        json.GetProperty("stale").GetBoolean().Should().BeTrue();
        json.GetProperty("asOf").GetString().Should().StartWith("2026-11-28");
        json.GetProperty("passed")[0].GetProperty("symbol").GetString().Should().Be("FPT");
        json.GetProperty("passed")[0].GetProperty("flags")[0].GetString().Should().Be("beta cao (2.1)");
        json.GetProperty("unverifiedHose")[0].GetProperty("symbol").GetString().Should().Be("VNM");
        json.GetProperty("excludedCounts").GetProperty("ROE thấp").GetInt32().Should().Be(3);
        json.GetProperty("disclaimer").GetString().Should().Contain("không phải khuyến nghị mua").And.Contain("không phải đánh giá rủi ro");
        json.GetRawText().ToLowerInvariant().Should().NotContain("score");
        json.TryGetProperty("reason", out var reason).Should().BeFalse("không có lý do thì vắng mặt");
        _ = reason;
    }

    [Fact]
    public void serialization_of_an_unavailable_result_carries_the_reason()
    {
        var json = JsonSerializer.SerializeToElement(
            new ScreenerResult { Status = "unavailable", Reason = "Dữ liệu sàng lọc quá cũ" }, McpJsonUtilities.DefaultOptions);

        json.GetProperty("status").GetString().Should().Be("unavailable");
        json.GetProperty("reason").GetString().Should().Be("Dữ liệu sàng lọc quá cũ");
        json.GetProperty("passed").GetArrayLength().Should().Be(0);
    }
}
