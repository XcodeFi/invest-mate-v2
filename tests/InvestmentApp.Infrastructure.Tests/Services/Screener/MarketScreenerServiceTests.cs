using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Screener;
using InvestmentApp.Infrastructure.Services.Screener;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Tests.Services.Screener;

public class MarketScreenerServiceTests
{
    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeSource : IScreenerDataSource
    {
        public int UniverseCalls;
        public int HoseCalls;
        public Func<ScreenerUniverseFetch> Universe = () => Ok(100);
        public Func<HoseLists> Hose = () => new HoseLists(Array.Empty<HoseStatusEntry>(), Array.Empty<HoseDelisting>());
        public TimeSpan Delay = TimeSpan.Zero;

        public async Task<ScreenerUniverseFetch> FetchUniverseAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref UniverseCalls);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            return Universe();
        }

        public Task<HoseLists> FetchHoseListsAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref HoseCalls);
            return Task.FromResult(Hose());
        }
    }

    private static ScreenerUniverseFetch Ok(int count, int invalid = 0, int rsOut = 0)
        => new(Enumerable.Range(0, count).Select(i => Row($"S{i:000}")).ToList(), count + invalid, invalid, rsOut);

    private static ScreenerRow Row(string symbol)
        => new(symbol, "HOSE", "8000", 2_000m, 20_000m, 1_000_000m, 20_000m, 10m, 1m, 15m, 5m, 100m, 60, 60, 60, 1m);

    private static readonly DateTimeOffset T0 = new(2026, 11, 28, 3, 0, 0, TimeSpan.Zero);

    private static (MarketScreenerService Sut, FakeSource Source, FakeTime Time) Build(Action<ScreenerOptions>? tweak = null)
    {
        var source = new FakeSource();
        var time = new FakeTime(T0);
        var options = new ScreenerOptions { MinRows = 10 };
        tweak?.Invoke(options);
        var sut = new MarketScreenerService(source, new ScreenerStateStore(), time, Options.Create(options), NullLogger<MarketScreenerService>.Instance);
        return (sut, source, time);
    }

    [Fact]
    public async Task caches_the_raw_universe_for_six_hours_then_refreshes()
    {
        var (sut, source, time) = Build();

        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(5.9));
        var inside = await sut.GetSnapshotAsync();
        source.UniverseCalls.Should().Be(1);
        inside.Stale.Should().BeFalse();

        time.Advance(TimeSpan.FromHours(0.2));
        var after = await sut.GetSnapshotAsync();
        source.UniverseCalls.Should().Be(2);
        after.AsOf.Should().Be(time.GetUtcNow());
    }

    [Fact]
    public async Task concurrent_requests_share_one_refresh_single_flight()
    {
        var (sut, source, _) = Build();
        source.Delay = TimeSpan.FromMilliseconds(150);

        var snapshots = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => sut.GetSnapshotAsync()));

        source.UniverseCalls.Should().Be(1);
        source.HoseCalls.Should().Be(1);
        snapshots.Should().OnlyContain(s => s.Rows.Count == 100);
    }

    [Fact]
    public async Task page_failure_fails_whole_refresh_serves_last_good_stale()
    {
        var (sut, source, time) = Build();
        var good = await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => throw new HttpRequestException("trang 3 lỗi");

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Stale.Should().BeTrue();
        snapshot.AsOf.Should().Be(good.AsOf, "AsOf là thời điểm của bản tốt, không phải bây giờ");
        snapshot.Rows.Should().HaveCount(100, "không bao giờ trả một phần của lần lấy dở");
        snapshot.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task no_partial_universe_silently()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        var calls = 0;
        source.Universe = () => { calls++; throw new FormatException("trang 2 hỏng"); };

        var snapshot = await sut.GetSnapshotAsync();

        calls.Should().Be(1);
        snapshot.Rows.Select(r => r.Symbol).Should().BeEquivalentTo(Enumerable.Range(0, 100).Select(i => $"S{i:000}"));
    }

    [Fact]
    public async Task sanity_check_failure_marks_unverified()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => Ok(60);   // −40% số dòng: ngoài ±20% so với lần tốt gần nhất

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Stale.Should().BeTrue();
        snapshot.Rows.Should().HaveCount(100, "bản không đạt kiểm tra không được nhận");
        snapshot.Notes.Should().Contain(n => n.Contains("không đạt kiểm tra"));
    }

    [Theory]
    [InlineData(81, true)]
    [InlineData(80, true)]    // đúng −20%: còn nằm trong ngưỡng
    [InlineData(79, false)]
    [InlineData(120, true)]   // đúng +20%
    [InlineData(121, false)]
    public async Task row_count_must_stay_within_20_percent_of_the_last_good_run(int next, bool accepted)
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => Ok(next);

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Rows.Count.Should().Be(accepted ? next : 100);
        snapshot.Stale.Should().Be(!accepted);
    }

    [Fact]
    public async Task an_rs_value_outside_1_to_99_rejects_the_whole_refresh()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => Ok(100, rsOut: 1);

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Stale.Should().BeTrue();
        snapshot.Notes.Should().Contain(n => n.Contains("không đạt kiểm tra"));
    }

    [Fact]
    public async Task too_many_unusable_rows_rejects_the_refresh()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => Ok(100, invalid: 30);   // 30/130 > 10%

        (await sut.GetSnapshotAsync()).Stale.Should().BeTrue();
    }

    [Fact]
    public async Task the_first_load_has_no_last_good_so_a_tiny_universe_is_rejected_and_reported_unavailable()
    {
        var (sut, source, _) = Build(o => o.MinRows = 300);
        source.Universe = () => Ok(100);

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Rows.Should().BeEmpty();
        snapshot.UnavailableReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task stale_older_than_48h_returns_no_result()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        source.Universe = () => throw new HttpRequestException("chặn");

        time.Advance(TimeSpan.FromHours(47));
        (await sut.GetSnapshotAsync()).Rows.Should().HaveCount(100, "47 giờ vẫn trong giới hạn, đánh dấu cũ");

        time.Advance(TimeSpan.FromHours(2));   // 49 giờ
        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Rows.Should().BeEmpty();
        snapshot.UnavailableReason.Should().Be("Dữ liệu sàng lọc quá cũ");
    }

    [Fact]
    public async Task the_first_load_failing_reports_unavailable_not_an_exception()
    {
        var (sut, source, _) = Build();
        source.Universe = () => throw new HttpRequestException("lỗi");

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Rows.Should().BeEmpty();
        snapshot.UnavailableReason.Should().Contain("Không lấy được");
    }

    [Fact]
    public async Task after_a_failure_the_source_is_not_hammered_for_the_backoff_window()
    {
        var (sut, source, time) = Build();
        source.Universe = () => throw new HttpRequestException("lỗi");

        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromMinutes(4));
        await sut.GetSnapshotAsync();
        source.UniverseCalls.Should().Be(1);

        time.Advance(TimeSpan.FromMinutes(2));
        await sut.GetSnapshotAsync();
        source.UniverseCalls.Should().Be(2);
    }

    // ---------------- Danh sách HOSE: bộ đệm riêng, lỗi thì đóng (không coi là sạch)

    [Fact]
    public async Task hose_lists_are_cached_separately_and_returned_with_the_snapshot()
    {
        var (sut, source, _) = Build();
        source.Hose = () => new HoseLists(new[] { new HoseStatusEntry("S001", "CK thuộc diện bị cảnh báo", "x", null) }, Array.Empty<HoseDelisting>());

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Hose.Should().NotBeNull();
        snapshot.Hose!.Statuses.Should().ContainSingle();
        snapshot.HoseAsOf.Should().Be(T0);
        snapshot.HoseStale.Should().BeFalse();
    }

    [Fact]
    public async Task hose_lists_failure_with_no_last_good_leaves_hose_null_without_failing_the_universe()
    {
        var (sut, source, _) = Build();
        source.Hose = () => throw new HttpRequestException("HOSE chặn");

        var snapshot = await sut.GetSnapshotAsync();

        snapshot.Hose.Should().BeNull("null nghĩa là chưa kiểm được, không phải rỗng");
        snapshot.Rows.Should().HaveCount(100);
        snapshot.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task hose_lists_failure_serves_the_last_good_marked_stale_until_48h()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        source.Hose = () => throw new HttpRequestException("HOSE chặn");

        time.Advance(TimeSpan.FromHours(7));
        var stale = await sut.GetSnapshotAsync();
        stale.Hose.Should().NotBeNull();
        stale.HoseStale.Should().BeTrue();

        time.Advance(TimeSpan.FromHours(43));   // 50 giờ kể từ lần tốt
        (await sut.GetSnapshotAsync()).Hose.Should().BeNull("quá 48 giờ thì không còn tin danh sách cũ: đóng");
    }

    [Fact]
    public async Task universe_failure_does_not_touch_the_hose_cache()
    {
        var (sut, source, time) = Build();
        await sut.GetSnapshotAsync();
        time.Advance(TimeSpan.FromHours(7));
        source.Universe = () => throw new HttpRequestException("lỗi");

        await sut.GetSnapshotAsync();

        source.HoseCalls.Should().Be(2, "HOSE có TTL riêng và vẫn làm mới được khi universe lỗi");
    }

    // ---------------- Ngưỡng không phải một phần của khoá lấy

    [Fact]
    public async Task thresholds_do_not_change_fetch_key()
    {
        var (sut, source, _) = Build();
        var service = (IMarketScreenerService)sut;

        foreach (var minRoe in new[] { 5m, 10m, 12m, 20m })
        foreach (var upcom in new[] { false, true })
        {
            var snap = await service.GetSnapshotAsync();
            ScreenerRules.Evaluate(snap, new ScreenerCriteria { MinRoe = minRoe, IncludeUpcom = upcom }.Clamped());
        }

        source.UniverseCalls.Should().Be(1, "nguồn chỉ được gọi một lần cho mọi tổ hợp ngưỡng");
        source.HoseCalls.Should().Be(1);
    }
}
