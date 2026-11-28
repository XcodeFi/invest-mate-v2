using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services.Signals;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class SignalSetupServiceTests
{
    private readonly Mock<IMarketDataProvider> _prices = new();
    private readonly Mock<IStockInfoProvider> _info = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    // 2026-11-30 12:00 giờ Việt Nam = 05:00 UTC
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 30, 5, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private SignalSetupService Sut(DateTimeOffset? now = null)
        => new(_prices.Object, _info.Object, _cache, new FixedTime(now ?? NowUtc));

    /// <summary>Chuỗi tổng hợp kết thúc đúng ngày <paramref name="lastDate"/>, mọi nến đã điều chỉnh.</summary>
    private static List<StockPriceData> Series(int n, DateTime lastDate, int seed = 11, bool adjusted = true)
    {
        var bars = PullbackBacktestTests.Synthetic(n, seed);
        var shift = lastDate.Date - bars[^1].Date;
        return bars.Select(b => new StockPriceData
        {
            Symbol = "FPT", Date = b.Date + shift, Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = 1000,
            IsAdjusted = adjusted, AdjustedAsOf = adjusted ? lastDate.Date : null
        }).ToList();
    }

    private void Prices(List<StockPriceData> bars)
        => _prices.Setup(p => p.GetHistoricalPricesAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bars);

    private void Exchange(string? exchange)
        => _info.Setup(i => i.GetStockDetailAsync("FPT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockDetailInfo { Symbol = "FPT", Exchange = exchange! });

    private static readonly DateTime Today = new(2026, 11, 30);

    // ───── không đủ dữ liệu: mặc định không có thống kê ─────

    [Fact]
    public async Task unadjusted_series_returns_reason_no_stats()
    {
        Prices(Series(700, Today.AddDays(-1), adjusted: false));
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.ReasonUnavailable.Should().Be("Chưa đủ lịch sử giá đã điều chỉnh để thống kê");
        dto.Setups.Should().BeEmpty();
        dto.Adjusted.Should().BeFalse();
    }

    [Fact]
    public async Task a_single_unadjusted_bar_among_adjusted_ones_is_enough_to_refuse()
    {
        var bars = Series(700, Today.AddDays(-1));
        bars[300].IsAdjusted = false;
        Prices(bars);
        Exchange("HOSE");

        (await Sut().GetSignalSetupsAsync("FPT")).ReasonUnavailable.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]     // nguồn dự phòng ~61 nến
    [InlineData(249)]
    public async Task short_series_returns_reason_no_stats(int n)
    {
        Prices(Series(Math.Max(n, 1), Today.AddDays(-1)).Take(n).ToList());
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.ReasonUnavailable.Should().NotBeNull();
        dto.Setups.Should().BeEmpty();
    }

    [Fact]
    public async Task exactly_250_bars_is_enough()
    {
        Prices(Series(250, Today.AddDays(-1)));
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.ReasonUnavailable.Should().BeNull();
        dto.Setups.Should().ContainSingle();
    }

    [Fact]
    public async Task stale_series_returns_reason_no_stats()
    {
        Prices(Series(700, Today.AddDays(-11)));   // nến cuối cách hôm nay 11 ngày lịch
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.ReasonUnavailable.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]   // nghỉ Tết dài liền: nến cuối cách 9 ngày lịch vẫn là bình thường
    [InlineData(10)]
    public async Task a_tet_shaped_gap_up_to_10_calendar_days_is_not_stale(int daysBehind)
    {
        Prices(Series(700, Today.AddDays(-daysBehind)));
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.ReasonUnavailable.Should().BeNull();
    }

    [Fact]
    public async Task staleness_uses_the_vietnam_date_not_the_utc_date()
    {
        // 20:00 UTC ngày 20/11 = 03:00 ngày 21/11 giờ Việt Nam.
        // Nến cuối 10/11: theo giờ VN là 11 ngày (cũ); nếu dùng ngày UTC thì chỉ 10 ngày (lọt). Nến cuối 11/11: 10 ngày VN, chưa cũ.
        var now = new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.Zero);
        Exchange("HOSE");

        Prices(Series(700, new DateTime(2026, 11, 10)));
        (await Sut(now).GetSignalSetupsAsync("FPT")).ReasonUnavailable.Should().NotBeNull();

        _cache.Remove("signals:FPT:20261121");
        Prices(Series(700, new DateTime(2026, 11, 11)));
        (await Sut(now).GetSignalSetupsAsync("FPT")).ReasonUnavailable.Should().BeNull();
    }

    [Fact]
    public async Task an_empty_or_failing_source_returns_the_reason_not_an_exception_with_no_cache()
    {
        _prices.Setup(p => p.GetHistoricalPricesAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPriceData>());
        Exchange("HOSE");
        var sut = Sut();

        (await sut.GetSignalSetupsAsync("FPT")).ReasonUnavailable.Should().NotBeNull();
        Prices(Series(700, Today.AddDays(-1)));
        (await sut.GetSignalSetupsAsync("FPT")).ReasonUnavailable.Should().BeNull("kết quả không có dữ liệu không được nhớ lại cả ngày");
    }

    // ───── có dữ liệu ─────

    [Fact]
    public async Task usable_series_returns_counts_and_the_fixed_text()
    {
        Prices(Series(745, Today.AddDays(-1)));
        Exchange("HOSE");

        var dto = await Sut().GetSignalSetupsAsync("FPT");

        dto.Symbol.Should().Be("FPT");
        dto.Adjusted.Should().BeTrue();
        dto.AdjustedAsOf.Should().Be(Today.AddDays(-1));
        dto.Disclaimer.Should().Be("Kết quả quá khứ, không đảm bảo tương lai");
        dto.Notes.Should().HaveCountGreaterThanOrEqualTo(4);
        dto.Notes.Should().Contain(n => n.Contains("điều chỉnh")).And.Contain(n => n.Contains("người sống sót"))
            .And.Contain(n => n.Contains("lệnh dừng")).And.Contain(n => n.Contains("T+2,5"));
        var setup = dto.Setups.Single();
        setup.Key.Should().Be("pullback");
        setup.Name.Should().Be("Pullback xu hướng");
        setup.Signals.Should().BeGreaterThan(0);
        setup.Completed.Should().Be(setup.N);
    }

    [Fact]
    public async Task n_below_30_hides_winrate_and_ev()
    {
        Prices(Series(745, Today.AddDays(-1)));
        Exchange("HOSE");

        var setup = (await Sut().GetSignalSetupsAsync("FPT")).Setups.Single();

        setup.N.Should().BeLessThan(30);
        setup.SampleSufficient.Should().BeFalse();
        setup.WinRate.Should().BeNull();
        setup.WinRateLow.Should().BeNull();
        setup.EvR.Should().BeNull();
        setup.EvRLow.Should().BeNull();
        setup.AvgWinR.Should().BeNull();
        setup.AvgLossR.Should().BeNull();
    }

    [Fact]
    public async Task n_at_least_30_returns_wilson_and_t_interval()
    {
        // chuỗi dài đủ để có ≥ 30 lệnh đã đóng
        var bars = Series(6000, Today.AddDays(-1), seed: 11);
        Prices(bars);
        Exchange("HOSE");

        var setup = (await Sut().GetSignalSetupsAsync("FPT")).Setups.Single();

        setup.N.Should().BeGreaterThanOrEqualTo(30, "chuỗi tổng hợp 6000 phiên phải cho ≥ 30 lệnh để test này có nghĩa");
        setup.SampleSufficient.Should().BeTrue();
        setup.WinRate.Should().NotBeNull();
        setup.WinRateLow.Should().BeLessThan(setup.WinRate!.Value);
        setup.WinRateHigh.Should().BeGreaterThan(setup.WinRate.Value);
        setup.EvR.Should().NotBeNull();
        setup.EvRLow.Should().BeLessThan(setup.EvR!.Value);
        setup.EvRHigh.Should().BeGreaterThan(setup.EvR.Value);
    }

    [Fact]
    public async Task the_dto_has_no_per_trade_list()
    {
        // Lean: không đưa danh sách từng lệnh ra (người dùng sẽ nhìn kết cục từng lệnh rồi tự đếm tỉ lệ thắng)
        typeof(SignalSetupDto).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Trades") || n.Contains("Outcomes"));
        typeof(SignalSetupsDto).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Trades") || n.Contains("Outcomes"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task the_result_is_cached_for_the_day_and_the_source_is_called_once()
    {
        Prices(Series(745, Today.AddDays(-1)));
        Exchange("HOSE");
        var sut = Sut();

        var first = await sut.GetSignalSetupsAsync("FPT");
        var second = await sut.GetSignalSetupsAsync("FPT");

        second.Should().BeSameAs(first);
        _prices.Verify(p => p.GetHistoricalPricesAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task the_cache_key_is_per_day()
    {
        Prices(Series(745, Today.AddDays(-1)));
        Exchange("HOSE");

        await Sut().GetSignalSetupsAsync("FPT");
        await Sut(NowUtc.AddDays(1)).GetSignalSetupsAsync("FPT");

        _prices.Verify(p => p.GetHistoricalPricesAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ───── sàn ─────

    [Theory]
    [InlineData("HOSE", 0.07)]
    [InlineData("HNX", 0.10)]
    [InlineData("UPCOM", 0.15)]
    public async Task the_exchange_from_the_stock_detail_picks_the_price_limit(string exchange, double limit)
    {
        Exchange(exchange);

        var rules = await Sut().ResolveRulesAsync("FPT", CancellationToken.None);

        rules.Limit.Should().Be((decimal)limit);
    }

    [Fact]
    public async Task unknown_exchange_defaults_to_7pct()
    {
        Exchange(null);
        (await Sut().ResolveRulesAsync("FPT", CancellationToken.None)).Limit.Should().Be(0.07m);

        _info.Setup(i => i.GetStockDetailAsync("FPT", It.IsAny<CancellationToken>())).ReturnsAsync((StockDetailInfo?)null);
        (await Sut().ResolveRulesAsync("FPT", CancellationToken.None)).Limit.Should().Be(0.07m);

        _info.Setup(i => i.GetStockDetailAsync("FPT", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));
        (await Sut().ResolveRulesAsync("FPT", CancellationToken.None)).Limit.Should().Be(0.07m, "tra sàn hỏng thì dùng 7%, không làm hỏng kết quả");
    }

    [Fact]
    public async Task the_exchange_is_looked_up_once_per_symbol_per_day()
    {
        Exchange("HNX");
        var sut = Sut();

        await sut.ResolveRulesAsync("FPT", CancellationToken.None);
        await sut.ResolveRulesAsync("FPT", CancellationToken.None);

        _info.Verify(i => i.GetStockDetailAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task a_failed_exchange_lookup_is_not_remembered_for_the_day()
    {
        _info.SetupSequence(i => i.GetStockDetailAsync("FPT", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"))
            .ReturnsAsync(new StockDetailInfo { Symbol = "FPT", Exchange = "UPCOM" });
        var sut = Sut();

        (await sut.ResolveRulesAsync("FPT", CancellationToken.None)).Limit.Should().Be(0.07m);
        (await sut.ResolveRulesAsync("FPT", CancellationToken.None)).Limit.Should().Be(0.15m);
    }
}
