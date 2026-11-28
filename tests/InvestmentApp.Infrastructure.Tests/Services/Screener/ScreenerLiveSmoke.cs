using FluentAssertions;
using InvestmentApp.Application.MarketData.Screener;
using InvestmentApp.Infrastructure.Services.Screener;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Tests.Services.Screener;

/// <summary>
/// Chạy thật vào hai nguồn công khai; CHỈ chạy khi đặt biến môi trường SCREENER_LIVE=1 (mặc định bỏ qua để bộ test không phụ thuộc mạng).
/// Dùng để kiểm lại đơn vị/hình dạng sau khi nguồn đổi: <c>SCREENER_LIVE=1 dotnet test --filter ScreenerLiveSmoke</c>.
/// </summary>
public class ScreenerLiveSmoke
{
    [Fact]
    public async Task live_universe_and_hose_lists_match_the_pinned_shape()
    {
        if (Environment.GetEnvironmentVariable("SCREENER_LIVE") != "1") return;

        var http = new HttpClient { BaseAddress = new Uri("https://api-finance-t19.24hmoney.vn"), Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        var source = new HmoneyScreenerDataSource(http, TimeProvider.System, Options.Create(new ScreenerOptions()));

        var universe = await source.FetchUniverseAsync();
        universe.Rows.Count.Should().BeGreaterThan(1000);
        universe.RsOutOfRangeCount.Should().Be(0);
        universe.InvalidCount.Should().BeLessThan(universe.RawCount / 10);

        var hose = await source.FetchHoseListsAsync();
        hose.Statuses.Count.Should().BeGreaterThan(0, "HOSE luôn có mã trong diện đặc biệt; rỗng nghĩa là nguồn đổi dạng");
        hose.Statuses.Select(s => s.ListName).Distinct().Count().Should().BeGreaterThan(0);

        var snapshot = new ScreenerSnapshot { Rows = universe.Rows, AsOf = DateTimeOffset.UtcNow, Hose = hose, HoseAsOf = DateTimeOffset.UtcNow };
        var result = ScreenerRules.Evaluate(snapshot, new ScreenerCriteria { IncludeExcluded = true }.Clamped());
        Console.WriteLine($"LIVE universe={universe.Rows.Count} raw={universe.RawCount} invalid={universe.InvalidCount} statuses={hose.Statuses.Count} delist={hose.Delistings.Count}");
        Console.WriteLine($"LIVE passed={result.Passed.Count} unverified={result.UnverifiedHose.Count} tradeoff={result.TradeoffCount}");
        foreach (var kv in result.ExcludedCounts.OrderByDescending(k => k.Value).Take(12)) Console.WriteLine($"LIVE excluded {kv.Key}: {kv.Value}");
        Console.WriteLine("LIVE sample passed: " + string.Join(",", result.Passed.Take(15).Select(p => p.Symbol)));
        _ = NullLogger.Instance;
    }
}
