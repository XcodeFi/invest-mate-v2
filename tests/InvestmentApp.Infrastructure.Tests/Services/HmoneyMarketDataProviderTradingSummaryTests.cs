using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Fixture là response THẬT bắt được từ api-finance-t19.24hmoney.vn ngày 2026-08-16 (mã HAH).
/// Upstream đã trả null cho change_3_month và change_6_month ở mọi mã. Khi upstream đổi tiếp,
/// bắt lại bằng curl và ghi đè Fixtures/Hmoney/trading_history_summary.json.
/// </summary>
public class HmoneyMarketDataProviderTradingSummaryTests
{
    private static string Fixture(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hmoney", name + ".json"));

    private static HmoneyMarketDataProvider CreateProvider(string responseBody)
    {
        var httpClient = new HttpClient(new FakeHttpHandler(responseBody))
        {
            BaseAddress = new Uri("https://api-test.example.com")
        };

        return new HmoneyMarketDataProvider(
            httpClient,
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<HmoneyMarketDataProvider>>().Object,
            Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com" }));
    }

    [Fact]
    public async Task GetTradingHistorySummaryAsync_KhiUpstreamTraNull_VanTraVeCacKhungThoiGianConSo()
    {
        var provider = CreateProvider(Fixture("trading_history_summary"));

        var result = await provider.GetTradingHistorySummaryAsync("HAH");

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HAH");
        result.ChangeDay.Should().Be(0.22m);
        result.ChangeWeek.Should().Be(-1.08m);
        result.ChangeMonth.Should().Be(-2.34m);
    }

    [Fact]
    public async Task GetTradingHistorySummaryAsync_KhungThoiGianUpstreamTraNull_KhongDuocQuyThanhKhong()
    {
        var provider = CreateProvider(Fixture("trading_history_summary"));

        var result = await provider.GetTradingHistorySummaryAsync("HAH");

        result!.Change3Month.Should().BeNull();
        result.Change6Month.Should().BeNull();
    }

    [Fact]
    public async Task GetTradingHistorySummaryAsync_KhiUpstreamCoDuSo_TraVeDungGiaTri()
    {
        var provider = CreateProvider(
            """{"message":"success","status":200,"data":{"change_day":0.22,"change_week":-1.08,"change_month":-2.34,"change_3_month":12.5,"change_6_month":-3.75}}""");

        var result = await provider.GetTradingHistorySummaryAsync("HAH");

        result!.Change3Month.Should().Be(12.5m);
        result.Change6Month.Should().Be(-3.75m);
    }

    private class FakeHttpHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
