using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Lịch sử NGÀY lấy từ UDF của biểu đồ web 24hmoney (nến thật, đã điều chỉnh, từ 2017); lỗi thì
/// dự phòng sang endpoint graph (chỉ có giá đóng cửa, chưa điều chỉnh) và đánh dấu IsAdjusted=false.
/// </summary>
public class HmoneyMarketDataProviderTradingViewTests
{
    private const string TvBase = "https://tv-test.example.com";

    private static long Epoch(int daysAgo) =>
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-daysAgo), TimeSpan.Zero).ToUnixTimeSeconds();

    private static string TvOk(int days)
    {
        string Arr(Func<int, string> f) => "[" + string.Join(",", Enumerable.Range(0, days).Select(f)) + "]";
        return "{\"symbol\":\"FPT\",\"s\":\"ok\",\"t\":" + Arr(i => Epoch(days - 1 - i).ToString()) +
               ",\"c\":" + Arr(_ => "50.5") + ",\"o\":" + Arr(_ => "50") + ",\"h\":" + Arr(_ => "51") +
               ",\"l\":" + Arr(_ => "49") + ",\"v\":" + Arr(_ => "1000000") + "}";
    }

    private static string Graph(int days)
    {
        var pts = Enumerable.Range(0, days).Select(i => "{\"x\":" + Epoch(days - 1 - i) + ",\"y\":45.9,\"z\":1000}");
        return "{\"message\":\"success\",\"status\":200,\"data\":{\"basic_price\":45.8,\"points\":[" + string.Join(",", pts) + "]}}";
    }

    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Tv = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        public Func<HttpRequestMessage, HttpResponseMessage> GraphResp = _ => Json(Graph(60));
        public List<string> TvUrls { get; } = new();
        public List<string> GraphUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var url = request.RequestUri!.ToString();
            if (url.Contains("/tradingview/")) { TvUrls.Add(url); return Task.FromResult(Tv(request)); }
            GraphUrls.Add(url);
            return Task.FromResult(GraphResp(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (HmoneyMarketDataProvider P, Handler H) Create(Action<Handler>? setup = null)
    {
        var h = new Handler();
        setup?.Invoke(h);
        var client = new HttpClient(h) { BaseAddress = new Uri("https://api-test.example.com") };
        var p = new HmoneyMarketDataProvider(client, new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<HmoneyMarketDataProvider>>().Object,
            Options.Create(new MarketDataProviderOptions
            {
                BaseUrl = "https://api-test.example.com",
                TradingViewBaseUrl = TvBase,
            }));
        return (p, h);
    }

    private static Task<List<StockPriceData>> Year(HmoneyMarketDataProvider p, string sym = "FPT") =>
        p.GetHistoricalPricesAsync(sym, DateTime.UtcNow.AddMonths(-12), DateTime.UtcNow);

    [Fact]
    public async Task uses_tradingview_when_ok()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)));

        var bars = await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddYears(-2), DateTime.UtcNow);

        bars.Should().HaveCount(300);
        bars.Should().OnlyContain(b => b.IsAdjusted);
        bars.Should().OnlyContain(b => b.High > b.Low, "nến thật, không còn O=H=L=C");
        bars[0].Close.Should().Be(50500m, "giá nghìn đồng ×1000");
        h.GraphUrls.Should().BeEmpty("nguồn mới ổn thì không gọi nguồn cũ");
    }

    [Fact]
    public async Task requests_tradingview_daily_from_configured_host_over_about_three_years()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(10)));

        await Year(p, "fpt");

        var url = h.TvUrls.Single();
        url.Should().StartWith(TvBase + "/tradingview/history?");
        url.Should().Contain("symbol=FPT").And.Contain("resolution=D");
        var from = long.Parse(Regex.Match(url, @"from=(\d+)").Groups[1].Value);
        var years = (DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(from).UtcDateTime).TotalDays / 365.25;
        years.Should().BeInRange(2.9, 3.1);
        url.Should().NotContain("device_id", "UDF không cần các tham số giả của API di động");
    }

    [Theory]
    [InlineData("status500")]
    [InlineData("throws")]
    [InlineData("no_data")]
    [InlineData("empty")]
    [InlineData("garbage")]
    public async Task falls_back_on_error_and_flags_unadjusted(string failure)
    {
        var (p, h) = Create(x => x.Tv = failure switch
        {
            "status500" => _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            "throws" => _ => throw new HttpRequestException("mạng đứt"),
            "no_data" => _ => Json("{\"s\":\"no_data\"}"),
            "empty" => _ => Json("{\"s\":\"ok\",\"t\":[],\"c\":[],\"o\":[],\"h\":[],\"l\":[],\"v\":[]}"),
            _ => _ => Json("<html>404</html>"),
        });

        var bars = await Year(p);

        bars.Should().HaveCount(60);
        bars.Should().OnlyContain(b => !b.IsAdjusted, "nguồn dự phòng chưa điều chỉnh");
        bars[0].Close.Should().Be(45900m);
        h.GraphUrls.Should().ContainSingle();
    }

    [Fact]
    public async Task fallback_daily_volume_is_normalised_to_shares()
    {
        var (p, _) = Create();   // graph trả z = 1000 cho mỗi phiên

        var bars = await Year(p);

        bars.Should().OnlyContain(b => b.Volume == 10_000, "z của graph ngày chỉ bằng 1/10 số cổ phiếu");
    }

    [Fact]
    public async Task intraday_volume_is_not_rescaled()
    {
        var (p, _) = Create();

        var bars = await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddHours(-6), DateTime.UtcNow);

        bars.Should().NotBeEmpty().And.OnlyContain(b => b.Volume == 1_000);
    }

    [Fact]
    public async Task http_error_status_falls_back_even_when_body_looks_valid()
    {
        var (p, _) = Create(x => x.Tv = _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(TvOk(300), Encoding.UTF8, "application/json")
        });

        var bars = await Year(p);

        bars.Should().OnlyContain(b => !b.IsAdjusted, "trạng thái HTTP lỗi thì không tin thân phản hồi, dù nó trông hợp lệ");
        bars.Should().HaveCount(60);
    }

    [Fact]
    public async Task falls_back_on_timeout()
    {
        var (p, _) = Create(x => x.Tv = _ => throw new TaskCanceledException("hết giờ"));

        var bars = await Year(p);

        bars.Should().HaveCount(60);
        bars.Should().OnlyContain(b => !b.IsAdjusted);
    }

    [Fact]
    public async Task caller_cancellation_is_not_swallowed_as_fallback()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(10)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddMonths(-12), DateTime.UtcNow, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        h.GraphUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task intraday_unchanged()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(10)));

        await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddHours(-6), DateTime.UtcNow);

        h.TvUrls.Should().BeEmpty();
        h.GraphUrls.Should().ContainSingle().Which.Should().Contain("type=1");
    }

    [Fact]
    public async Task cache_hit_no_second_call()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)));

        var month = await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var year = await Year(p);

        h.TvUrls.Should().ContainSingle();
        month.Count.Should().BeLessThan(year.Count, "chuỗi cache là chuỗi CHƯA lọc");
        year.Count.Should().BeGreaterThan(200);
    }

    [Fact]
    public async Task fallback_result_is_cached_so_a_broken_source_is_not_hammered()
    {
        var (p, h) = Create();

        await Year(p);
        await Year(p);

        h.TvUrls.Should().ContainSingle();
        h.GraphUrls.Should().ContainSingle();
    }

    [Fact]
    public async Task both_sources_empty_is_not_cached()
    {
        var (p, h) = Create(x => x.GraphResp = _ =>
            Json("{\"message\":\"success\",\"status\":200,\"data\":{\"basic_price\":0,\"points\":[]}}"));

        var first = await Year(p);
        await Year(p);

        first.Should().BeEmpty();
        h.TvUrls.Should().HaveCount(2, "chuỗi rỗng có thể chỉ là trục trặc nhất thời");
    }

    [Fact]
    public async Task different_symbols_do_not_share_cache()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(10)));

        await Year(p, "FPT");
        await Year(p, "VNM");

        h.TvUrls.Should().HaveCount(2);
    }
}
