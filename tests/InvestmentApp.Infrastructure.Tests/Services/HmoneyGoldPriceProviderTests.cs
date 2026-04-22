using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class HmoneyGoldPriceProviderTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "hmoney_gia_vang_page.html");

    private static string LoadFixture() => File.ReadAllText(FixturePath);

    // =============================================
    // Parse tests (static method — no HTTP)
    // =============================================

    [Fact]
    public async Task ParseHtml_Fixture_Returns8EntriesAfterFilter()
    {
        // Fixture captured 2026-04-22: 2 SJC + 1 DOJI + 1 PNJ + 4 KHÁC (miếng + nhẫn only).
        // Nữ trang + Trang sức phải bị filter.
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        result.Should().HaveCount(8);
    }

    [Fact]
    public async Task ParseHtml_Fixture_FiltersNuTrangAndTrangSuc()
    {
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        // Chỉ có Miếng + Nhẫn — không có loại khác
        result.Should().OnlyContain(p => p.Type == GoldType.Mieng || p.Type == GoldType.Nhan);
    }

    [Fact]
    public async Task ParseHtml_Fixture_SJCMieng_HasCorrectPrices()
    {
        // Ground truth từ fixture: SJC Miếng → Mua 167,200,000, Bán 169,700,000
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);
        var sjcMieng = result.Single(p => p.Brand == GoldBrand.SJC && p.Type == GoldType.Mieng);

        sjcMieng.BuyPrice.Should().Be(167_200_000m);
        sjcMieng.SellPrice.Should().Be(169_700_000m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_SJCNhan_HasCorrectPrices()
    {
        // SJC Nhẫn → Mua 167,000,000, Bán 169,500,000
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);
        var sjcNhan = result.Single(p => p.Brand == GoldBrand.SJC && p.Type == GoldType.Nhan);

        sjcNhan.BuyPrice.Should().Be(167_000_000m);
        sjcNhan.SellPrice.Should().Be(169_500_000m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_DOJINhan_HasCorrectPrices()
    {
        // DOJI Nhẫn → Mua 166,700,000, Bán 169,700,000
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);
        var dojiNhan = result.Single(p => p.Brand == GoldBrand.DOJI && p.Type == GoldType.Nhan);

        dojiNhan.BuyPrice.Should().Be(166_700_000m);
        dojiNhan.SellPrice.Should().Be(169_700_000m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_PNJNhan_HasCorrectPrices()
    {
        // PNJ Nhẫn → Mua 166,500,000, Bán 169,500,000
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);
        var pnjNhan = result.Single(p => p.Brand == GoldBrand.PNJ && p.Type == GoldType.Nhan);

        pnjNhan.BuyPrice.Should().Be(166_500_000m);
        pnjNhan.SellPrice.Should().Be(169_500_000m);
    }

    [Fact]
    public async Task ParseHtml_Fixture_KhacSection_MapsToOther()
    {
        // KHÁC section: BTMC Miếng + BTMH Nhẫn + Ngọc Hải Nhẫn + Mi Hồng Nhẫn → 4 entries, tất cả Brand=Other
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        var others = result.Where(p => p.Brand == GoldBrand.Other).ToList();
        others.Should().HaveCount(4);
        others.Should().Contain(p => p.Type == GoldType.Mieng); // BTMC Miếng
        others.Where(p => p.Type == GoldType.Nhan).Should().HaveCount(3); // BTMH, Ngọc Hải, Mi Hồng
    }

    [Fact]
    public async Task ParseHtml_Fixture_PricesAreFullVND_NotScaledBy1000()
    {
        // Despite UI label "triệu VNĐ/lượng", giá trị HTML là full VND (VD: 167,200,000).
        // Sanity check: tất cả giá trong range 100M - 200M VND (không phải 100K - 200K nếu scaled sai).
        var html = LoadFixture();

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, CancellationToken.None);

        result.Should().OnlyContain(p => p.BuyPrice >= 100_000_000m && p.BuyPrice <= 200_000_000m);
        result.Should().OnlyContain(p => p.SellPrice >= 100_000_000m && p.SellPrice <= 200_000_000m);
    }

    [Fact]
    public async Task ParseHtml_EmptyHtml_ReturnsEmptyList()
    {
        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync("<html><body></body></html>", DateTime.UtcNow, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHtml_CrawlTime_SetsUpdatedAtOnAllEntries()
    {
        var html = LoadFixture();
        var expectedTime = new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc);

        var result = await HmoneyGoldPriceProvider.ParseHtmlAsync(html, expectedTime, CancellationToken.None);

        result.Should().OnlyContain(p => p.UpdatedAt == expectedTime);
    }

    // =============================================
    // End-to-end: HTTP + parse + cache
    // =============================================

    [Fact]
    public async Task GetPricesAsync_SuccessfulFetch_ReturnsPrices()
    {
        var provider = CreateProvider(new FixtureHttpHandler());

        var result = await provider.GetPricesAsync(CancellationToken.None);

        result.Should().HaveCount(8);
    }

    [Fact]
    public async Task GetPricesAsync_TwoCalls_OnlyOneHttpRequest_CacheHit()
    {
        var handler = new FixtureHttpHandler();
        var provider = CreateProvider(handler);

        await provider.GetPricesAsync(CancellationToken.None);
        await provider.GetPricesAsync(CancellationToken.None);

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPricesAsync_HttpFailure_NoStaleCache_Throws()
    {
        var handler = new ErrorHttpHandler();
        var provider = CreateProvider(handler);

        var act = () => provider.GetPricesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetPricesAsync_HttpFailureAfterSuccess_ReturnsStaleCache()
    {
        // Regression: plan requires stale fallback vì vàng update chậm, 5-phút error 500 là UX xấu.
        var handler = new ToggleHttpHandler(File.ReadAllText(FixturePath));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(handler, cache);

        // Successful fetch → populate stale cache
        var first = await provider.GetPricesAsync(CancellationToken.None);
        first.Should().HaveCount(8);

        // Invalidate fresh cache (simulate TTL expired) but keep stale
        cache.Remove("hmoney_gold_prices");
        handler.FailNextRequest = true;

        // Second call: fetch fails → should fall back to stale instead of throwing
        var second = await provider.GetPricesAsync(CancellationToken.None);
        second.Should().HaveCount(8);
    }

    [Fact]
    public async Task ParseHtmlAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var html = LoadFixture();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => HmoneyGoldPriceProvider.ParseHtmlAsync(html, DateTime.UtcNow, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetPriceAsync_SJCMieng_ReturnsMatching()
    {
        var provider = CreateProvider(new FixtureHttpHandler());

        var result = await provider.GetPriceAsync(GoldBrand.SJC, GoldType.Mieng, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Brand.Should().Be(GoldBrand.SJC);
        result.Type.Should().Be(GoldType.Mieng);
        result.BuyPrice.Should().Be(167_200_000m);
        result.SellPrice.Should().Be(169_700_000m);
    }

    // =============================================
    // Helpers
    // =============================================

    private static HmoneyGoldPriceProvider CreateProvider(HttpMessageHandler handler, IMemoryCache? cache = null)
    {
        var httpClient = new HttpClient(handler);
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HmoneyGoldPriceProvider>>();
        var options = Options.Create(new GoldPriceProviderOptions
        {
            PageUrl = "https://24hmoney.vn/gia-vang",
            CacheTtlMinutes = 5,
        });
        return new HmoneyGoldPriceProvider(httpClient, cache, logger, options);
    }

    /// <summary>HTTP handler trả fixture HTML. Đếm số request để test cache hit.</summary>
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

    /// <summary>Handler có thể toggle success/error để test stale fallback flow.</summary>
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
