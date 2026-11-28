using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Lấy trước chuỗi giá đã điều chỉnh (warm-up) ở phía nguồn giá: không bao giờ dùng graph dự phòng, không gọi khi nguồn đang trục trặc,
/// không đụng chuỗi lỗi dùng chung của biểu đồ người dùng, và bị chặn bởi ngân sách số lần gọi mỗi ngày.
/// </summary>
public class HmoneyProviderWarmupTests
{
    private const string TvBase = "https://tv-test.example.com";
    private static readonly DateTimeOffset Morning = new(2026, 11, 30, 2, 0, 0, TimeSpan.Zero);   // 09:00 giờ Việt Nam

    private static string TvOk(int days = 300)
    {
        string Arr(Func<int, string> f) => "[" + string.Join(",", Enumerable.Range(0, days).Select(f)) + "]";
        long Epoch(int back) => new DateTimeOffset(Morning.UtcDateTime.Date.AddDays(-back), TimeSpan.Zero).ToUnixTimeSeconds();
        return "{\"s\":\"ok\",\"t\":" + Arr(i => Epoch(days - 1 - i).ToString()) + ",\"c\":" + Arr(_ => "50.5") + ",\"o\":" + Arr(_ => "50") +
               ",\"h\":" + Arr(_ => "51") + ",\"l\":" + Arr(_ => "49") + ",\"v\":" + Arr(_ => "1000000") + "}";
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Waf() => Json("<!DOCTYPE html><html><body>Error. This page cannot be displayed.</body></html>");
    private static HttpResponseMessage NoData() => Json("{\"s\":\"no_data\"}");

    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Tv = _ => Json(TvOk());
        public List<string> TvUrls { get; } = new();
        public List<string> GraphUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/tradingview/")) { TvUrls.Add(url); return Task.FromResult(Tv(request)); }
            GraphUrls.Add(url);
            return Task.FromResult(Json("{\"status\":200,\"data\":{\"points\":[]}}"));
        }
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
        public int SaveCalls { get; private set; }
        public Task<AdjustedPriceSeriesSnapshot?> GetAsync(string symbol, CancellationToken ct = default) => Task.FromResult(Data.GetValueOrDefault(symbol));
        public Task SaveAsync(string symbol, DateTime fetchedAtUtc, IReadOnlyList<StockPriceData> bars, CancellationToken ct = default)
        {
            SaveCalls++;
            Data[symbol] = new AdjustedPriceSeriesSnapshot(symbol, fetchedAtUtc, bars.ToList());
            return Task.CompletedTask;
        }
        public Task<IReadOnlyDictionary<string, DateTime>> GetFetchedAtAsync(IReadOnlyCollection<string> symbols, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, DateTime>>(Data.Where(kv => symbols.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value.FetchedAtUtc));
    }

    private sealed class FakeBudget(int allowed) : IWarmupCallBudget
    {
        public int Consumed { get; private set; }
        public Task<bool> TryConsumeAsync(DateTime vnDay, int dailyCap, CancellationToken ct = default)
        {
            if (Consumed >= allowed) return Task.FromResult(false);
            Consumed++;
            return Task.FromResult(true);
        }
    }

    private static int _uniq;

    /// <summary>Cổng theo mã của nguồn là tĩnh (dùng chung giữa các instance, đúng như khi chạy thật): mỗi test dùng mã riêng để các test chạy song song không giành cổng của nhau.</summary>
    private sealed record Rig(HmoneyMarketDataProvider P, Handler H, FakeStore Store, FakeBudget Budget, FakeTime Time, IMemoryCache Cache)
    {
        private readonly int _id = Interlocked.Increment(ref _uniq);
        public string S(string name) => name + "Z" + _id;
    }

    private static Rig Create(Action<Handler>? setup = null, int budget = 30)
    {
        var h = new Handler();
        setup?.Invoke(h);
        var store = new FakeStore();
        var bud = new FakeBudget(budget);
        var time = new FakeTime(Morning);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var p = new HmoneyMarketDataProvider(
            new HttpClient(h) { BaseAddress = new Uri("https://api-test.example.com") }, cache,
            NullLogger<HmoneyMarketDataProvider>.Instance,
            Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com", TradingViewBaseUrl = TvBase }),
            store, time, bud);
        return new Rig(P: p, H: h, Store: store, Budget: bud, Time: time, Cache: cache);
    }

    private static Task<List<StockPriceData>> UserChart(HmoneyMarketDataProvider p, string symbol)
        => p.GetHistoricalPricesAsync(symbol, Morning.UtcDateTime.AddYears(-2), Morning.UtcDateTime);

    // ───── hành vi cơ bản ─────

    [Fact]
    public async Task warm_fetches_the_adjusted_series_and_persists_it()
    {
        var r = Create();

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Fetched);

        r.H.TvUrls.Should().ContainSingle();
        r.Store.Data.Should().ContainKey(r.S("FPT"));
        r.Store.Data[r.S("FPT")].Bars.Should().HaveCount(300);
        r.Budget.Consumed.Should().Be(1);
    }

    [Fact]
    public async Task warm_never_takes_the_graph_fallback()
    {
        var r = Create(h => h.Tv = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Failed);

        r.H.GraphUrls.Should().BeEmpty("warm-up không dùng graph dự phòng: chuỗi graph chưa điều chỉnh sẽ không bao giờ được lưu");
        r.Store.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task warm_skips_a_symbol_the_cache_already_answers()
    {
        var r = Create();
        await UserChart(r.P, r.S("FPT"));   // người dùng mở biểu đồ: chuỗi tốt nằm trong bộ nhớ đệm
        r.H.TvUrls.Clear();

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Skipped);

        r.H.TvUrls.Should().BeEmpty();
        r.Budget.Consumed.Should().Be(0, "không tốn lượt cho một cuộc gọi bộ nhớ đệm sẽ trả lời");
    }

    [Fact]
    public async Task warm_skips_a_symbol_whose_persisted_series_is_still_fresh()
    {
        var r = Create();
        var bars = (await UserChart(r.P, r.S("FPT"))).ToList();   // lấy một chuỗi hợp lệ để lưu
        r.H.TvUrls.Clear();
        var fresh = Create();                                    // bộ nhớ đệm trống, chỉ có bản lưu
        fresh.Store.Data[fresh.S("FPT")] = new AdjustedPriceSeriesSnapshot(fresh.S("FPT"), Morning.UtcDateTime.AddHours(-1), bars);

        (await fresh.P.WarmAsync(fresh.S("FPT"))).Should().Be(WarmFetchResult.Skipped);

        fresh.H.TvUrls.Should().BeEmpty();
        fresh.Budget.Consumed.Should().Be(0, "bản lưu còn mới thì không tốn lượt gọi");
    }

    [Fact]
    public async Task warm_is_degraded_by_the_global_cooldown_even_with_no_failure_streak()
    {
        // một lần thành công đặt lại chuỗi lỗi trong khi mốc nghỉ chung 15 phút còn chạy: nhánh cooldown phải tự đứng được, không dựa vào chuỗi lỗi
        var r = Create();
        var timed = typeof(HmoneyMarketDataProvider).GetNestedType("Timed`1", System.Reflection.BindingFlags.NonPublic)!.MakeGenericType(typeof(bool));
        r.Cache.Set("tv:cooldown:global", Activator.CreateInstance(timed, true, Morning.AddMinutes(10)));
        r.Cache.TryGetValue("tv:fail-streak", out object? _).Should().BeFalse("không có chuỗi lỗi nào");

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Degraded);

        r.H.TvUrls.Should().BeEmpty();
        r.Budget.Consumed.Should().Be(0);
    }

    [Fact]
    public async Task warm_ignores_a_malformed_symbol_without_calling()
    {
        var r = Create();

        (await r.P.WarmAsync("../etc")).Should().Be(WarmFetchResult.Skipped);

        r.H.TvUrls.Should().BeEmpty();
    }

    // ───── ngân sách ─────

    [Fact]
    public async Task warm_is_capped_by_the_persisted_daily_call_budget()
    {
        var r = Create(budget: 2);

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Fetched);
        (await r.P.WarmAsync(r.S("VNM"))).Should().Be(WarmFetchResult.Fetched);
        (await r.P.WarmAsync(r.S("HPG"))).Should().Be(WarmFetchResult.BudgetExhausted);

        r.H.TvUrls.Should().HaveCount(2, "lượt thứ ba bị chặn trước khi gọi nguồn");
    }

    [Fact]
    public async Task a_failed_call_still_costs_budget()
    {
        var r = Create(h => h.Tv = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), budget: 5);

        await r.P.WarmAsync(r.S("FPT"));

        r.Budget.Consumed.Should().Be(1);
    }

    // ───── không làm hại biểu đồ người dùng ─────

    [Fact]
    public async Task warm_makes_no_call_when_the_global_pause_is_active()
    {
        var r = Create(h => h.Tv = _ => Waf());
        foreach (var s in new[] { "AAA", "BBB", "CCC" }) await UserChart(r.P, r.S(s));   // 3 lỗi cả nguồn liên tiếp: cooldown chung
        var before = r.H.TvUrls.Count;

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Degraded);

        r.H.TvUrls.Should().HaveCount(before);
        r.Budget.Consumed.Should().Be(0);
    }

    [Fact]
    public async Task warm_makes_no_call_when_the_shared_streak_is_at_least_one()
    {
        var r = Create(h => h.Tv = _ => Waf());
        await UserChart(r.P, r.S("AAA"));   // một lỗi cả nguồn: chuỗi lỗi = 1, chưa tới ngưỡng cooldown chung
        var before = r.H.TvUrls.Count;

        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Degraded);

        r.H.TvUrls.Should().HaveCount(before);
    }

    [Fact]
    public async Task a_warm_failure_does_not_count_toward_the_shared_pause_threshold()
    {
        var r = Create(h => h.Tv = _ => Waf());
        await r.P.WarmAsync(r.S("WM0"));               // một lỗi cả nguồn của warm-up
        await UserChart(r.P, r.S("AAA"));              // hai lỗi cả nguồn của biểu đồ người dùng: chuỗi lỗi chung = 2, dưới ngưỡng 3
        await UserChart(r.P, r.S("BBB"));
        var before = r.H.TvUrls.Count;

        await UserChart(r.P, r.S("CCC"));

        r.H.TvUrls.Count.Should().Be(before + 1, "nếu lỗi warm-up được cộng vào chuỗi chung thì lần thứ ba đã bị cooldown chung chặn");
    }

    [Fact]
    public async Task a_warm_source_failure_pauses_warmup_itself_but_a_symbol_failure_does_not()
    {
        var r = Create(h => h.Tv = _ => Waf());
        (await r.P.WarmAsync(r.S("FPT"))).Should().Be(WarmFetchResult.Failed);
        (await r.P.WarmAsync(r.S("VNM"))).Should().Be(WarmFetchResult.Degraded, "warm-up tự nghỉ sau lỗi cả nguồn");

        var r2 = Create(h => h.Tv = _ => NoData());
        (await r2.P.WarmAsync(r2.S("FPT"))).Should().Be(WarmFetchResult.Failed);
        r2.H.Tv = _ => Json(TvOk());
        (await r2.P.WarmAsync(r2.S("FPT"))).Should().Be(WarmFetchResult.Skipped, "mã hỏng nghỉ tới hết ngày, không gọi lại");
        (await r2.P.WarmAsync(r2.S("VNM"))).Should().Be(WarmFetchResult.Fetched, "mã khác không bị ảnh hưởng");
    }

    [Fact]
    public async Task a_warm_failure_does_not_rest_the_symbol_for_user_charts()
    {
        var r = Create(h => h.Tv = _ => NoData());
        await r.P.WarmAsync(r.S("FPT"));
        r.H.Tv = _ => Json(TvOk());
        var before = r.H.TvUrls.Count;

        var bars = await UserChart(r.P, r.S("FPT"));

        r.H.TvUrls.Count.Should().Be(before + 1);
        bars.Should().NotBeEmpty();
    }

    [Fact]
    public async Task warm_success_populates_the_good_cache_so_the_next_user_chart_makes_no_call()
    {
        var r = Create();
        await r.P.WarmAsync(r.S("FPT"));
        var calls = r.H.TvUrls.Count;

        (await UserChart(r.P, r.S("FPT"))).Should().NotBeEmpty();

        r.H.TvUrls.Count.Should().Be(calls);
    }

    // ───── độ tươi ─────

    [Fact]
    public void IsFresh_follows_the_provider_rule_same_vn_day_and_before_1510()
    {
        var r = Create();

        r.P.IsFresh(Morning.UtcDateTime.AddHours(-1)).Should().BeTrue();    // 08:00 giờ VN hôm nay, chưa tới 15:10
        r.P.IsFresh(Morning.UtcDateTime.AddDays(-1)).Should().BeFalse();    // hôm qua
        r.Time.Advance(TimeSpan.FromHours(7));                              // 16:00 giờ VN
        r.P.IsFresh(Morning.UtcDateTime).Should().BeFalse("lấy lúc 09:00, qua mốc đóng cửa 15:10 thì cần lấy lại");
    }
}
