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

    private static (HmoneyMarketDataProvider P, Handler H) Create(
        Action<Handler>? setup = null, IAdjustedPriceSeriesStore? store = null, TimeProvider? time = null,
        Mock<ILogger<HmoneyMarketDataProvider>>? logger = null)
    {
        var h = new Handler();
        setup?.Invoke(h);
        var client = new HttpClient(h) { BaseAddress = new Uri("https://api-test.example.com") };
        var p = new HmoneyMarketDataProvider(client, new MemoryCache(new MemoryCacheOptions()),
            (logger ?? new Mock<ILogger<HmoneyMarketDataProvider>>()).Object,
            Options.Create(new MarketDataProviderOptions
            {
                BaseUrl = "https://api-test.example.com",
                TradingViewBaseUrl = TvBase,
            }), store, time);
        return (p, h);
    }

    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeStore : IAdjustedPriceSeriesStore
    {
        public Dictionary<string, AdjustedPriceSeriesSnapshot> Data { get; } = new();
        public int GetCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public bool ThrowOnGet { get; set; }
        public bool ThrowOnSave { get; set; }

        public Task<AdjustedPriceSeriesSnapshot?> GetAsync(string symbol, CancellationToken ct = default)
        {
            GetCalls++;
            if (ThrowOnGet) throw new InvalidOperationException("mongo hỏng");
            return Task.FromResult(Data.GetValueOrDefault(symbol));
        }

        public Task SaveAsync(string symbol, DateTime fetchedAtUtc, IReadOnlyList<StockPriceData> bars, CancellationToken ct = default)
        {
            SaveCalls++;
            if (ThrowOnSave) throw new InvalidOperationException("mongo hỏng");
            Data[symbol] = new AdjustedPriceSeriesSnapshot(symbol, fetchedAtUtc, bars.ToList());
            return Task.CompletedTask;
        }
    }

    private static AdjustedPriceSeriesSnapshot Snapshot(string symbol, DateTime fetchedAtUtc, int bars = 300, decimal close = 63_000m)
    {
        var last = DateTime.UtcNow.Date;
        return new AdjustedPriceSeriesSnapshot(symbol, fetchedAtUtc, Enumerable.Range(0, bars).Select(i => new StockPriceData
        {
            Symbol = symbol, Date = last.AddDays(-(bars - 1 - i)), Open = close, High = close + 500, Low = close - 500,
            Close = close, Volume = 1, IsAdjusted = true
        }).ToList());
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> HtmlWaf() =>
        _ => Json("<!DOCTYPE html><html><body>Error. This page can't be displayed. The incident ID is: N/A.</body></html>");

    private static void VerifyWarning(Mock<ILogger<HmoneyMarketDataProvider>> logger, string contains, Times times) =>
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(contains)), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

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
    public async Task both_sources_empty_is_not_cached_but_udf_still_rests()
    {
        var (p, h) = Create(x => x.GraphResp = _ =>
            Json("{\"message\":\"success\",\"status\":200,\"data\":{\"basic_price\":0,\"points\":[]}}"));

        var first = await Year(p);
        await Year(p);

        first.Should().BeEmpty();
        h.GraphUrls.Should().HaveCount(2, "chuỗi rỗng có thể chỉ là trục trặc nhất thời nên không đệm");
        h.TvUrls.Should().ContainSingle("UDF vừa lỗi thì nghỉ, không bị gọi lại ngay");
    }

    [Fact]
    public async Task different_symbols_do_not_share_cache()
    {
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(10)));

        await Year(p, "FPT");
        await Year(p, "VNM");

        h.TvUrls.Should().HaveCount(2);
    }

    // ───────────── Gọi UDF thưa, lưu bản tốt cuối cùng, nghỉ khi lỗi ─────────────

    private static readonly DateTimeOffset Morning = new(2026, 11, 30, 2, 0, 0, TimeSpan.Zero);     // 09:00 giờ Việt Nam
    private static readonly DateTimeOffset Afternoon = new(2026, 11, 30, 9, 0, 0, TimeSpan.Zero);   // 16:00 giờ Việt Nam, sau giờ đóng cửa

    [Fact]
    public async Task persisted_series_fetched_today_is_used_without_calling_udf()
    {
        var time = new FakeTime(Morning);
        var store = new FakeStore();
        store.Data["FPT"] = Snapshot("FPT", Morning.UtcDateTime.AddHours(-1), close: 63_000m);
        var (p, h) = Create(store: store, time: time);

        var bars = await Year(p);

        h.TvUrls.Should().BeEmpty("đã lấy thành công hôm nay thì không gọi UDF nữa, kể cả sau khi khởi động lại");
        h.GraphUrls.Should().BeEmpty();
        bars.Should().NotBeEmpty().And.OnlyContain(b => b.IsAdjusted && b.Close == 63_000m);
    }

    [Fact]
    public async Task udf_success_is_persisted_and_served_again_without_new_calls()
    {
        var store = new FakeStore();
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store, new FakeTime(Morning));

        await Year(p);
        await Year(p);

        h.TvUrls.Should().ContainSingle();
        store.SaveCalls.Should().Be(1);
        store.Data["FPT"].Bars.Should().HaveCount(300);
        store.Data["FPT"].FetchedAtUtc.Should().Be(Morning.UtcDateTime);
    }

    [Fact]
    public async Task udf_is_called_at_most_once_per_symbol_per_vietnam_day()
    {
        var time = new FakeTime(Afternoon);   // 16:00 VN: đã sau giờ đóng cửa
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), new FakeStore(), time);

        await Year(p);
        time.Advance(TimeSpan.FromHours(5));    // 21:00 VN, cùng ngày
        await Year(p);
        h.TvUrls.Should().ContainSingle("cùng một ngày giao dịch, chuỗi đã lấy sau giờ đóng cửa");

        time.Advance(TimeSpan.FromHours(5));    // 02:00 VN hôm sau
        await Year(p);
        h.TvUrls.Should().HaveCount(2, "sang ngày mới thì được lấy lại");
    }

    [Fact]
    public async Task udf_is_called_once_per_day_even_when_there_is_no_persistent_store()
    {
        var time = new FakeTime(Afternoon);
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store: null, time: time);

        await Year(p);
        time.Advance(TimeSpan.FromHours(5));   // 21:00 VN, cùng ngày
        await Year(p);

        h.TvUrls.Should().ContainSingle("bộ nhớ đệm chuỗi tốt sống tới hết ngày Việt Nam");
    }

    [Fact]
    public async Task html_with_http_200_counts_as_failure_and_starts_a_cooldown()
    {
        var time = new FakeTime(Morning);
        var (p, h) = Create(x => x.Tv = HtmlWaf(), new FakeStore(), time);

        var first = await Year(p);
        first.Should().OnlyContain(b => !b.IsAdjusted);

        time.Advance(TimeSpan.FromMinutes(5));   // bộ nhớ đệm dự phòng (60 giây) đã hết
        var second = await Year(p);

        second.Should().OnlyContain(b => !b.IsAdjusted);
        h.TvUrls.Should().ContainSingle("đang nghỉ 30 phút, không gọi lại UDF");
        h.GraphUrls.Should().HaveCount(2, "chuỗi dự phòng chỉ đệm ngắn");
    }

    [Fact]
    public async Task symbol_cooldown_ends_after_the_configured_minutes()
    {
        var time = new FakeTime(Morning);
        var (p, h) = Create(x => x.Tv = HtmlWaf(), new FakeStore(), time);

        await Year(p);
        time.Advance(TimeSpan.FromMinutes(29));
        await Year(p);
        h.TvUrls.Should().ContainSingle();

        time.Advance(TimeSpan.FromMinutes(2));   // 31 phút
        await Year(p);

        h.TvUrls.Should().HaveCount(2, "hết nghỉ thì thử đúng một lần");
    }

    [Fact]
    public async Task several_failures_in_a_row_pause_udf_for_every_symbol()
    {
        var time = new FakeTime(Morning);
        var (p, h) = Create(x => x.Tv = HtmlWaf(), new FakeStore(), time);

        await Year(p, "AAA"); await Year(p, "BBB"); await Year(p, "CCC");
        await Year(p, "DDD");

        h.TvUrls.Should().HaveCount(3, "ba lần lỗi liên tiếp thì UDF nghỉ chung, mã thứ tư không bị gọi");

        time.Advance(TimeSpan.FromMinutes(16));
        await Year(p, "DDD");

        h.TvUrls.Should().HaveCount(4, "hết nghỉ chung thì thăm dò một lần");
    }

    [Fact]
    public async Task a_success_resets_the_failure_streak()
    {
        var time = new FakeTime(Morning);
        var (p, h) = Create(x => x.Tv = req => req.RequestUri!.Query.Contains("symbol=OKK")
            ? Json(TvOk(300)) : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), new FakeStore(), time);

        await Year(p, "AAA"); await Year(p, "BBB");   // 2 lỗi
        await Year(p, "OKK");                        // thành công: về 0
        await Year(p, "CCC"); await Year(p, "DDD");   // 2 lỗi nữa, vẫn dưới ngưỡng 3
        await Year(p, "EEE");

        h.TvUrls.Should().HaveCount(6, "chuỗi lỗi bị cắt bởi lần thành công nên chưa chạm ngưỡng chung");
    }

    [Fact]
    public async Task failure_serves_the_last_good_persisted_series_never_the_flat_fallback()
    {
        var store = new FakeStore();
        var time = new FakeTime(Morning);
        var ok = true;
        var (p, h) = Create(x => x.Tv = req => ok ? Json(TvOk(300)) : HtmlWaf()(req), store, time);
        var day1 = await Year(p);
        day1.Should().OnlyContain(b => b.IsAdjusted);

        time.Advance(TimeSpan.FromHours(24));   // ngày hôm sau: chuỗi trong ngày hết hạn
        ok = false;
        var day2 = await Year(p);

        day2.Should().HaveCount(day1.Count).And.OnlyContain(b => b.IsAdjusted, "vẫn là chuỗi đã điều chỉnh gần nhất");
        h.GraphUrls.Should().BeEmpty("chuỗi dự phòng không được ghi đè hay thay thế một chuỗi tốt");
    }

    [Fact]
    public async Task a_cached_fallback_never_shadows_a_good_series_persisted_meanwhile()
    {
        var store = new FakeStore();
        var time = new FakeTime(Morning);
        var (p, _) = Create(x => x.Tv = HtmlWaf(), store, time);
        var first = await Year(p);
        first.Should().OnlyContain(b => !b.IsAdjusted);   // UDF hỏng, chưa có bản lưu: dự phòng graph

        // Một phiên bản khác của dịch vụ lấy được chuỗi tốt và lưu lại trong lúc đó.
        store.Data["FPT"] = Snapshot("FPT", Morning.UtcDateTime.AddSeconds(5));
        time.Advance(TimeSpan.FromSeconds(10));   // dự phòng (60 giây) chưa hết hạn
        var second = await Year(p);

        second.Should().NotBeEmpty().And.OnlyContain(b => b.IsAdjusted, "chuỗi dự phòng không được che chuỗi tốt");
    }

    [Fact]
    public async Task persisted_series_older_than_a_day_is_flagged_in_the_log()
    {
        var store = new FakeStore();
        var time = new FakeTime(Morning);
        store.Data["FPT"] = Snapshot("FPT", Morning.UtcDateTime.AddDays(-3));
        var logger = new Mock<ILogger<HmoneyMarketDataProvider>>();
        var (p, _) = Create(x => x.Tv = HtmlWaf(), store, time, logger);

        var bars = await Year(p);

        bars.Should().NotBeEmpty().And.OnlyContain(b => b.IsAdjusted);
        VerifyWarning(logger, "3 days old", Times.Once());
    }

    [Fact]
    public async Task persisted_series_from_yesterday_is_not_flagged()
    {
        var store = new FakeStore();
        var time = new FakeTime(Morning);
        store.Data["FPT"] = Snapshot("FPT", Morning.UtcDateTime.AddDays(-1));
        var logger = new Mock<ILogger<HmoneyMarketDataProvider>>();
        var (p, _) = Create(x => x.Tv = HtmlWaf(), store, time, logger);

        await Year(p);

        VerifyWarning(logger, "days old", Times.Never());
    }

    [Fact]
    public async Task no_persisted_series_and_udf_down_uses_graph_flagged_unadjusted()
    {
        var (p, h) = Create(x => x.Tv = HtmlWaf(), new FakeStore(), new FakeTime(Morning));

        var bars = await Year(p);

        bars.Should().HaveCount(60).And.OnlyContain(b => !b.IsAdjusted);
        h.GraphUrls.Should().ContainSingle();
    }

    [Fact]
    public async Task failure_to_save_does_not_lose_the_series_just_fetched()
    {
        var store = new FakeStore { ThrowOnSave = true };
        var (p, _) = Create(x => x.Tv = _ => Json(TvOk(300)), store, new FakeTime(Morning));

        var bars = await Year(p);

        bars.Should().OnlyContain(b => b.IsAdjusted);
        bars.Count.Should().BeGreaterThan(200);
    }

    [Fact]
    public async Task failure_to_read_the_store_still_tries_udf()
    {
        var store = new FakeStore { ThrowOnGet = true };
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store, new FakeTime(Morning));

        var bars = await Year(p);

        h.TvUrls.Should().ContainSingle();
        bars.Should().OnlyContain(b => b.IsAdjusted);
    }

    [Fact]
    public async Task concurrent_requests_for_one_symbol_cause_one_udf_call()
    {
        var store = new FakeStore();
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store, new FakeTime(Morning));

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Year(p)));

        h.TvUrls.Should().ContainSingle();
        store.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task intraday_never_touches_udf_store_or_cooldown()
    {
        var store = new FakeStore();
        var (p, h) = Create(x => x.Tv = HtmlWaf(), store, new FakeTime(Morning));

        await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddHours(-6), DateTime.UtcNow);

        h.TvUrls.Should().BeEmpty();
        store.GetCalls.Should().Be(0);
    }

    // ───────────── Mốc đóng cửa: chuỗi lấy trước giờ đóng cửa được lấy lại đúng một lần ─────────────

    [Fact]
    public async Task a_series_fetched_before_the_close_is_refetched_once_after_the_close_then_not_again()
    {
        var time = new FakeTime(Morning.AddMinutes(30));    // 09:30 VN
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), new FakeStore(), time);

        await Year(p);
        time.Advance(TimeSpan.FromHours(5));                 // 14:30 VN, trong phiên
        await Year(p);
        h.TvUrls.Should().ContainSingle("trong phiên vẫn dùng chuỗi đã lấy");

        time.Advance(TimeSpan.FromHours(1));                 // 15:30 VN, sau giờ đóng cửa
        await Year(p);
        h.TvUrls.Should().HaveCount(2, "nến phiên hôm nay đã chốt nên lấy lại một lần");

        time.Advance(TimeSpan.FromMinutes(10));              // 15:40 VN
        await Year(p);
        h.TvUrls.Should().HaveCount(2, "ngân sách tối đa 2 lần mỗi mã mỗi ngày");
    }

    [Fact]
    public async Task a_persisted_series_fetched_before_the_close_is_refetched_after_the_close_even_after_a_restart()
    {
        var time = new FakeTime(Morning.AddHours(6).AddMinutes(30));   // 15:30 VN
        var store = new FakeStore();
        store.Data["FPT"] = Snapshot("FPT", Morning.UtcDateTime.AddMinutes(30));   // lấy lúc 09:30 VN
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store, time);

        await Year(p);

        h.TvUrls.Should().ContainSingle();
        store.Data["FPT"].FetchedAtUtc.Should().Be(time.GetUtcNow().UtcDateTime, "bản lưu được làm mới");
    }

    [Fact]
    public async Task a_persisted_series_fetched_after_the_close_is_used_without_a_new_call()
    {
        var time = new FakeTime(Afternoon.AddHours(2));     // 18:00 VN
        var store = new FakeStore();
        store.Data["FPT"] = Snapshot("FPT", Afternoon.UtcDateTime);   // lấy lúc 16:00 VN
        var (p, h) = Create(x => x.Tv = _ => Json(TvOk(300)), store, time);

        await Year(p);

        h.TvUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task refetch_after_the_close_that_fails_keeps_serving_the_morning_series()
    {
        var time = new FakeTime(Morning.AddMinutes(30));
        var ok = true;
        var (p, h) = Create(x => x.Tv = req => ok ? Json(TvOk(300)) : HtmlWaf()(req), new FakeStore(), time);
        var morning = await Year(p);

        time.Advance(TimeSpan.FromHours(6));                 // 15:30 VN, nguồn đã chặn
        ok = false;
        var afternoon = await Year(p);

        afternoon.Should().HaveCount(morning.Count).And.OnlyContain(b => b.IsAdjusted);
        h.GraphUrls.Should().BeEmpty("không rơi xuống chuỗi dẹt khi còn chuỗi tốt đã lưu");
    }

    // ───────────── no_data (mã nguồn không có) khác với bị chặn ─────────────

    [Fact]
    public async Task no_data_for_several_symbols_does_not_pause_udf_for_everyone()
    {
        var (p, h) = Create(x => x.Tv = _ => Json("{\"s\":\"no_data\"}"), new FakeStore(), new FakeTime(Morning));

        await Year(p, "VNINDEX"); await Year(p, "AAA"); await Year(p, "BBB");
        await Year(p, "CCC");

        h.TvUrls.Should().HaveCount(4, "no_data không phải dấu hiệu bị chặn nên không kéo cả nguồn nghỉ chung");
    }

    [Fact]
    public async Task no_data_still_rests_that_symbol_so_it_is_not_asked_again()
    {
        var time = new FakeTime(Morning);
        var (p, h) = Create(x => x.Tv = _ => Json("{\"s\":\"no_data\"}"), new FakeStore(), time);

        await Year(p, "VNINDEX");
        time.Advance(TimeSpan.FromMinutes(5));
        await Year(p, "VNINDEX");

        h.TvUrls.Should().ContainSingle();
    }

    [Fact]
    public async Task no_data_does_not_reset_the_streak_of_real_failures()
    {
        var (p, h) = Create(x => x.Tv = req => req.RequestUri!.Query.Contains("symbol=ND")
            ? Json("{\"s\":\"no_data\"}") : HtmlWaf()(req), new FakeStore(), new FakeTime(Morning));

        await Year(p, "AAA"); await Year(p, "BBB");   // 2 lỗi chặn
        await Year(p, "ND");                          // no_data: không đổi chuỗi lỗi
        await Year(p, "CCC");                         // lỗi thứ 3 → nghỉ chung
        await Year(p, "DDD");

        h.TvUrls.Should().HaveCount(4, "AAA, BBB, ND, CCC; mã DDD bị nghỉ chung chặn lại");
    }

    // ───────────── adjustedAsOf: ngày nến cuối của chuỗi đã điều chỉnh đang được phục vụ ─────────────

    [Fact]
    public async Task every_bar_of_a_fresh_udf_series_carries_the_date_of_its_last_bar()
    {
        var (p, _) = Create(x => x.Tv = _ => Json(TvOk(300)), new FakeStore(), new FakeTime(Morning));

        var bars = await Year(p);

        bars.Should().NotBeEmpty().And.OnlyContain(b => b.AdjustedAsOf == DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task the_date_is_that_of_the_series_even_when_the_requested_range_ends_earlier()
    {
        var (p, _) = Create(x => x.Tv = _ => Json(TvOk(300)), new FakeStore(), new FakeTime(Morning));

        var bars = await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-30));

        bars.Should().NotBeEmpty();
        bars.Should().OnlyContain(b => b.AdjustedAsOf == DateTime.UtcNow.Date,
            "người dùng hỏi một khoảng cũ thì độ tươi vẫn là của cả chuỗi, không phải của khoảng đó");
    }

    [Fact]
    public async Task a_stale_persisted_series_reports_its_own_old_last_bar_date()
    {
        var time = new FakeTime(Morning);
        var store = new FakeStore();
        var snap = Snapshot("FPT", Morning.UtcDateTime.AddDays(-4));
        // bản lưu cũ: nến cuối cách đây 4 ngày, và (như dữ liệu thật trong kho) chưa mang trường AdjustedAsOf
        var old = snap.Bars.Select(b => new StockPriceData
        {
            Symbol = b.Symbol, Date = b.Date.AddDays(-4), Open = b.Open, High = b.High, Low = b.Low, Close = b.Close,
            Volume = b.Volume, IsAdjusted = true
        }).ToList();
        store.Data["FPT"] = new AdjustedPriceSeriesSnapshot("FPT", snap.FetchedAtUtc, old);
        var (p, _) = Create(x => x.Tv = HtmlWaf(), store, time);

        var bars = await p.GetHistoricalPricesAsync("FPT", DateTime.UtcNow.AddYears(-2), DateTime.UtcNow);

        bars.Should().NotBeEmpty();
        bars.Should().OnlyContain(b => b.AdjustedAsOf == DateTime.UtcNow.Date.AddDays(-4));
    }

    [Fact]
    public async Task graph_fallback_bars_have_no_adjusted_date()
    {
        var (p, _) = Create(x => x.Tv = HtmlWaf(), new FakeStore(), new FakeTime(Morning));

        var bars = await Year(p);

        bars.Should().NotBeEmpty().And.OnlyContain(b => b.AdjustedAsOf == null);
    }
}
