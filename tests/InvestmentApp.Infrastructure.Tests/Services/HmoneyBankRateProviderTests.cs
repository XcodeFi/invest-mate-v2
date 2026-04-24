using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class HmoneyBankRateProviderTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "hmoney_lai_suat_page.html");

    private static string LoadFixture() => File.ReadAllText(FixturePath);

    // =============================================
    // Parse tests (static method — no HTTP)
    // =============================================

    [Fact]
    public async Task ParseHtml_Fixture_ReturnsTopRatePerTerm_FromOnlineTable()
    {
        // Plan D9: prefer online table over counter (online thường cao hơn 0.2-0.8%).
        // Fixture 2026-03-25 online table: SHB 12T = 7.6% (top across all banks, online kênh).
        var html = LoadFixture();

        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        snapshot.TopByTerm.Should().ContainKeys(1, 3, 6, 9, 12);
        snapshot.TopByTerm[12].RatePercent.Should().Be(7.6m);
        snapshot.TopByTerm[12].BankName.Should().Contain("SHB");
    }

    [Fact]
    public async Task ParseHtml_Fixture_TopRatesAllReasonableVNRange()
    {
        // Sanity: top rate cho mỗi term phải nằm trong range 2-15%/năm. VN hiện tại (2026) ~4-8%.
        var html = LoadFixture();

        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        snapshot.TopByTerm.Values.Should().OnlyContain(e => e.RatePercent > 0m && e.RatePercent < 15m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_ExtractsSourceTimestamp()
    {
        // Fixture có dòng "Cập nhật lúc: 23:59:59 25/03/2026".
        var html = LoadFixture();

        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        snapshot.SourceTimestamp.Should().NotBeNull();
        snapshot.SourceTimestamp!.Value.Year.Should().Be(2026);
        snapshot.SourceTimestamp.Value.Month.Should().Be(3);
        snapshot.SourceTimestamp.Value.Day.Should().Be(25);
    }

    [Fact]
    public async Task ParseHtml_Fixture_SkipsDashRates()
    {
        // TPBank trong fixture có cột 9 tháng = "-" (không công bố). Parser phải skip row này cho term 9T,
        // không throw và không count "-" là 0.
        var html = LoadFixture();

        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        // Top 9T không được là 0 (nếu bug parse "-" thành 0 sẽ sai)
        snapshot.TopByTerm[9].RatePercent.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_FetchedAt_SetToCrawlTime()
    {
        var crawlTime = new DateTime(2026, 4, 24, 10, 30, 0, DateTimeKind.Utc);
        var html = LoadFixture();

        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync(html, crawlTime, CancellationToken.None);

        snapshot.FetchedAt.Should().Be(crawlTime);
    }

    [Fact]
    public async Task ParseHtml_EmptyHtml_ReturnsEmptySnapshot()
    {
        var snapshot = await HmoneyBankRateProvider.ParseHtmlAsync("<html><body></body></html>", DateTime.UtcNow, CancellationToken.None);
        snapshot.TopByTerm.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHtmlAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var html = LoadFixture();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => HmoneyBankRateProvider.ParseHtmlAsync(html, DateTime.UtcNow, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =============================================
    // End-to-end: HTTP + parse + cache
    // =============================================

    [Fact]
    public async Task GetTopRatesAsync_SuccessfulFetch_ReturnsSnapshot()
    {
        var provider = CreateProvider(new FixtureHttpHandler());

        var snapshot = await provider.GetTopRatesAsync(CancellationToken.None);

        snapshot.TopByTerm.Should().NotBeEmpty();
        snapshot.TopByTerm[12].RatePercent.Should().Be(7.6m);
    }

    [Fact]
    public async Task GetTopRatesAsync_TwoCalls_OnlyOneHttpRequest_CacheHit()
    {
        var handler = new FixtureHttpHandler();
        var provider = CreateProvider(handler);

        await provider.GetTopRatesAsync(CancellationToken.None);
        await provider.GetTopRatesAsync(CancellationToken.None);

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTopRatesAsync_HttpFailure_NoStaleCache_Throws()
    {
        var handler = new ErrorHttpHandler();
        var provider = CreateProvider(handler);

        var act = () => provider.GetTopRatesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetTopRatesAsync_HttpFailureAfterSuccess_ReturnsStaleCache()
    {
        var handler = new ToggleHttpHandler(File.ReadAllText(FixturePath));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(handler, cache);

        // Successful fetch → populate stale cache
        var first = await provider.GetTopRatesAsync(CancellationToken.None);
        first.TopByTerm.Should().NotBeEmpty();

        // Invalidate fresh cache (simulate TTL expired) but keep stale
        cache.Remove("hmoney_bank_rates");
        handler.FailNextRequest = true;

        // Second call: fetch fails → should fall back to stale instead of throwing
        var second = await provider.GetTopRatesAsync(CancellationToken.None);
        second.TopByTerm.Should().NotBeEmpty();
    }

    // =============================================
    // Helpers
    // =============================================

    private static HmoneyBankRateProvider CreateProvider(HttpMessageHandler handler, IMemoryCache? cache = null)
    {
        var httpClient = new HttpClient(handler);
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HmoneyBankRateProvider>>();
        var options = Options.Create(new BankRateProviderOptions
        {
            PageUrl = "https://24hmoney.vn/lai-suat-gui-ngan-hang",
            FreshCacheHours = 6,
            StaleCacheHours = 24,
        });
        return new HmoneyBankRateProvider(httpClient, cache, logger, options);
    }

    private class FixtureHttpHandler : HttpMessageHandler
    {
        private readonly string _html;
        public int RequestCount { get; private set; }

        public FixtureHttpHandler() : this(File.ReadAllText(FixturePath)) { }
        public FixtureHttpHandler(string html) { _html = html; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html"),
            });
        }
    }

    private class ToggleHttpHandler : HttpMessageHandler
    {
        private readonly string _html;
        public bool FailNextRequest { get; set; }

        public ToggleHttpHandler(string html) { _html = html; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FailNextRequest)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html"),
            });
        }
    }

    private class ErrorHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
