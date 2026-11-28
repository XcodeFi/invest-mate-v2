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

    private static AdjustedPriceSeriesSnapshot Snapshot(string symbol, int seed, int bars, DateTime lastDate, bool adjusted = true)
    {
        var raw = PullbackBacktestTests.Synthetic(bars, seed);
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
}
