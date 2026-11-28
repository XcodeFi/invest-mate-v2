using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Constants;
using InvestmentApp.Infrastructure.Services.Signals;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class SignalBaseRateServiceTests
{
    private readonly Mock<IAdjustedPriceSeriesStore> _store = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    // 2026-11-30 20:00 giờ Việt Nam = 13:00 UTC (sau giờ đóng cửa nên nến hôm nay, nếu có, là nến đã đóng)
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 30, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Today = new(2026, 11, 30);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private SignalBaseRateService Sut(DateTimeOffset? now = null) => new(_store.Object, _cache, new FixedTime(now ?? NowUtc));

    private static AdjustedPriceSeriesSnapshot Snapshot(string symbol, int seed, int bars, DateTime lastDate, bool adjusted = true, decimal scale = 1m)
    {
        var raw = PullbackBacktestTests.Synthetic(bars, seed, scale);
        var shift = lastDate.Date - raw[^1].Date;
        return new AdjustedPriceSeriesSnapshot(symbol, lastDate, raw.Select(b => new StockPriceData
        {
            Symbol = symbol, Date = b.Date + shift, Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = 1000, IsAdjusted = adjusted
        }).ToList());
    }

    /// <summary>Đặt chuỗi cho các mã VN30 theo thứ tự rổ: <paramref name="make"/> trả null thì mã đó chưa có bản lưu.</summary>
    private void Setup(Func<int, string, AdjustedPriceSeriesSnapshot?> make)
    {
        var i = 0;
        foreach (var symbol in Vn30Basket.Symbols)
        {
            var snap = make(i++, symbol);
            _store.Setup(s => s.GetAsync(symbol, It.IsAny<CancellationToken>())).ReturnsAsync(snap);
        }
    }

    private static AdjustedPriceSeriesSnapshot Good(int i, string symbol) => Snapshot(symbol, seed: 11 + i, bars: 1000, lastDate: Today.AddDays(-1));

    private static int ExpectedTrades(AdjustedPriceSeriesSnapshot s)
        => PullbackBacktest.Run(s.Bars.Select(b => new Bar(b.Date, b.Open, b.High, b.Low, b.Close)).ToList(), MarketRules.For("HOSE"))
            .Trades.Count(t => t.Completed && t.R != null);

    // ───── kết quả ─────

    [Fact]
    public async Task pools_the_completed_trades_of_all_usable_symbols()
    {
        Setup((i, s) => Good(i, s));
        var expected = Vn30Basket.Symbols.Select((s, i) => ExpectedTrades(Good(i, s))).Sum();

        var result = await Sut().GetPullbackBaseRateAsync();

        result.Reason.Should().BeNull();
        result.BaseRate.Should().NotBeNull();
        result.BaseRate!.K.Should().Be(30);
        result.BaseRate.N.Should().Be(expected);
        result.BaseRate.Label.Should().Be("Tỉ lệ nền VN30");
        result.BaseRate.AsOf.Should().Be(Today.AddDays(-1));
        result.BaseRate.EffectiveMonths.Should().BeGreaterThanOrEqualTo(24);
        result.BaseRate.WinRateLow.Should().BeLessThanOrEqualTo(result.BaseRate.WinRate);
        result.BaseRate.WinRateHigh.Should().BeGreaterThanOrEqualTo(result.BaseRate.WinRate);
    }

    [Fact]
    public async Task the_result_carries_the_mandatory_notes()
    {
        Setup((i, s) => Good(i, s));

        var notes = (await Sut().GetPullbackBaseRateAsync()).BaseRate!.Notes;

        notes.Should().Contain(n => n.Contains("xấp xỉ") && n.Contains("kéo dài qua nhiều tháng"));
        notes.Should().Contain(n => n.Contains("không phải bằng chứng riêng của mã này"));
        notes.Should().Contain(n => n.Contains("còn niêm yết"));
    }

    [Fact]
    public async Task only_fresh_adjusted_250bar_snapshots_used()
    {
        Setup((i, s) => i switch
        {
            < 5 => Snapshot(s, 11 + i, bars: 200, lastDate: Today.AddDays(-1)),                       // quá ngắn
            < 8 => Snapshot(s, 11 + i, bars: 1000, lastDate: Today.AddDays(-11)),                     // cũ hơn 10 ngày lịch
            < 10 => Snapshot(s, 11 + i, bars: 1000, lastDate: Today.AddDays(-1), adjusted: false),    // chưa điều chỉnh
            < 12 => null,                                                                             // chưa có bản lưu
            _ => Good(i, s)
        });

        var result = await Sut().GetPullbackBaseRateAsync();

        result.BaseRate!.K.Should().Be(18);
        var expected = Vn30Basket.Symbols.Select((s, i) => (s, i)).Where(x => x.i >= 12).Sum(x => ExpectedTrades(Good(x.i, x.s)));
        result.BaseRate.N.Should().Be(expected, "chỉ lệnh của các mã đủ điều kiện được gộp");
    }

    [Fact]
    public async Task base_rate_null_when_k_below_15()
    {
        Setup((i, s) => i < 14 ? Good(i, s) : null);

        var result = await Sut().GetPullbackBaseRateAsync();

        result.BaseRate.Should().BeNull();
        result.Reason.Should().Contain("14/30");
    }

    [Fact]
    public async Task the_forming_bar_of_today_is_left_out_before_1500()
    {
        // 12:00 giờ VN: nến hôm nay đang hình thành, bị bỏ; K vẫn tính theo nến đã đóng
        var morning = new DateTimeOffset(2026, 11, 30, 5, 0, 0, TimeSpan.Zero);
        Setup((i, s) => Snapshot(s, 11 + i, bars: 1000, lastDate: Today));

        var before = await Sut(morning).GetPullbackBaseRateAsync();
        var after = await new SignalBaseRateService(_store.Object, new MemoryCache(new MemoryCacheOptions()), new FixedTime(NowUtc)).GetPullbackBaseRateAsync();

        before.BaseRate!.AsOf.Should().Be(Today.AddDays(-1));
        after.BaseRate!.AsOf.Should().Be(Today);
    }

    // ───── nhớ kết quả ─────

    [Fact]
    public async Task the_result_is_remembered_so_the_store_is_read_once_per_period()
    {
        Setup((i, s) => Good(i, s));
        var sut = Sut();

        await sut.GetPullbackBaseRateAsync();
        await sut.GetPullbackBaseRateAsync();

        _store.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(30));
    }

    [Fact]
    public async Task never_calls_a_price_provider_only_the_store()
    {
        typeof(SignalBaseRateService).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType)
            .Should().NotContain(t => typeof(IMarketDataProvider).IsAssignableFrom(t) || typeof(IStockInfoProvider).IsAssignableFrom(t));
        await Task.CompletedTask;
    }
    // ───── hàm gộp (Pool) ─────

    private static SignalBaseRateService.PooledOutcome PoolGood(int count = 30)
        => SignalBaseRateService.Pool(Vn30Basket.Symbols.Take(count).Select((s, i) => (AdjustedPriceSeriesSnapshot?)Good(i, s)), NowUtc);

    [Fact]
    public void the_basket_uses_the_HOSE_rules()
    {
        // fixture tổng hợp không phân biệt được R giữa các bộ luật, nên ghim chính bộ luật được dùng (VN30 toàn HOSE: biên độ 7%, bước giá HOSE)
        SignalBaseRateService.BasketRules.Limit.Should().Be(MarketRules.For("HOSE").Limit);
        SignalBaseRateService.BasketRules.Limit.Should().NotBe(MarketRules.For("UPCOM").Limit);
        SignalBaseRateService.BasketRules.Tick(30_000m).Should().Be(MarketRules.For("HOSE").Tick(30_000m));
    }

    [Fact]
    public void pool_leaves_out_open_trades()
    {
        // tìm một chuỗi mà tại điểm cắt đang có lệnh mở
        AdjustedPriceSeriesSnapshot? withOpen = null;
        for (var bars = 600; bars < 760 && withOpen == null; bars++)
        {
            var snap = Snapshot("VNM", 11, bars, Today.AddDays(-1));
            var run = PullbackBacktest.Run(snap.Bars.Select(b => new Bar(b.Date, b.Open, b.High, b.Low, b.Close)).ToList(), MarketRules.For("HOSE"));
            if (run.Trades.Any(t => !t.Completed)) withOpen = snap;
        }
        withOpen.Should().NotBeNull("fixture phải có lệnh đang mở");

        var pooled = SignalBaseRateService.Pool(new[] { (AdjustedPriceSeriesSnapshot?)withOpen }, NowUtc);

        pooled.N.Should().Be(ExpectedTrades(withOpen!), "lệnh đang mở không được gộp");
    }

    [Fact]
    public void pool_takes_AsOf_from_the_latest_bar_across_symbols()
    {
        var older = Snapshot("VNM", 11, 1000, Today.AddDays(-4));
        var newer = Snapshot("FPT", 12, 1000, Today.AddDays(-2));

        SignalBaseRateService.Pool(new AdjustedPriceSeriesSnapshot?[] { older, newer }, NowUtc).AsOf.Should().Be(Today.AddDays(-2));
        SignalBaseRateService.Pool(new AdjustedPriceSeriesSnapshot?[] { newer, older }, NowUtc).AsOf.Should().Be(Today.AddDays(-2));
    }

    [Fact]
    public void pool_interval_is_pinned_for_a_fixed_fixture()
    {
        // ghim thuật toán + hạt giống + số lần lấy mẫu: đổi bất kỳ thứ nào trong đó sẽ làm khoảng này đổi
        var stats = PoolGood().Stats!;

        stats.N.Should().Be(580);
        stats.WinRate.Should().Be(0.7052m);
        stats.WinRateLow.Should().Be(0.6667m);
        stats.WinRateHigh.Should().Be(0.7411m);
    }

    // ───── hạn nhớ kết quả ─────

    [Fact]
    public async Task a_result_computed_before_1510_is_not_served_after_it()
    {
        Setup((i, s) => Good(i, s));
        var at1400 = new DateTimeOffset(2026, 11, 30, 7, 0, 0, TimeSpan.Zero);
        var at1530 = new DateTimeOffset(2026, 11, 30, 8, 30, 0, TimeSpan.Zero);

        await Sut(at1400).GetPullbackBaseRateAsync();
        await Sut(at1530).GetPullbackBaseRateAsync();

        _store.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(60));
    }

    [Fact]
    public async Task an_insufficient_result_is_remembered_only_briefly_because_warmup_keeps_adding_symbols()
    {
        Setup((i, s) => i < 14 ? Good(i, s) : null);

        await Sut(NowUtc).GetPullbackBaseRateAsync();
        await Sut(NowUtc.AddMinutes(10)).GetPullbackBaseRateAsync();
        _store.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(30), "trong 30 phút vẫn dùng kết quả nhớ");

        await Sut(NowUtc.AddMinutes(31)).GetPullbackBaseRateAsync();
        _store.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(60), "qua 30 phút phải đọc lại chuỗi đã lưu");
    }
}
