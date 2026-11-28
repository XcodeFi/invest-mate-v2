using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Constants;
using InvestmentApp.Infrastructure.Services.Signals;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class SignalSeriesWarmupTests
{
    private readonly Mock<IAdjustedPriceSeriesStore> _store = new();
    private readonly Mock<IAdjustedSeriesWarmer> _warmer = new();
    private readonly Dictionary<string, DateTime> _fetchedAt = new();
    private readonly List<string> _warmed = new();

    private static readonly DateTime Now = new(2026, 11, 30, 2, 0, 0, DateTimeKind.Utc);

    public SignalSeriesWarmupTests()
    {
        _store.Setup(s => s.GetFetchedAtAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fetchedAt);
        // IsFresh của nguồn: lấy trong vòng 6 giờ qua thì còn tươi (thay cho luật 15:10 thật, kiểm riêng ở test của nguồn)
        _warmer.Setup(w => w.IsFresh(It.IsAny<DateTime>())).Returns((DateTime d) => Now - d < TimeSpan.FromHours(6));
        _warmer.Setup(w => w.WarmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => { _warmed.Add(s); return WarmFetchResult.Fetched; });
    }

    private SignalSeriesWarmup Sut(int perRun = 1, bool enabled = true)
        => new(_store.Object, _warmer.Object,
            Options.Create(new SignalWarmupOptions { Enabled = enabled, SymbolsPerRun = perRun }), NullLogger<SignalSeriesWarmup>.Instance);

    private void AllFetchedLongAgo()
    {
        foreach (var s in Vn30Basket.Symbols) _fetchedAt[s] = Now.AddDays(-2);
    }

    [Fact]
    public async Task warmup_picks_missing_then_oldest_vn30_symbol()
    {
        AllFetchedLongAgo();
        _fetchedAt["VNM"] = Now.AddDays(-9);     // cũ nhất
        _fetchedAt.Remove("HPG");                // chưa có chuỗi: đi trước mọi mã đã có

        await Sut().WarmNextAsync();
        _warmed.Should().Equal("HPG");

        _warmed.Clear();
        _fetchedAt["HPG"] = Now.AddHours(-30);   // HPG đã có nhưng chưa phải cũ nhất
        await Sut().WarmNextAsync();
        _warmed.Should().Equal("VNM");
    }

    [Fact]
    public async Task missing_symbols_keep_the_basket_order()
    {
        AllFetchedLongAgo();
        _fetchedAt.Remove("VCB");
        _fetchedAt.Remove("ACB");

        await Sut(perRun: 2).WarmNextAsync();

        _warmed.Should().Equal("ACB", "VCB");
    }

    [Fact]
    public async Task warmup_skips_a_snapshot_the_provider_considers_fresh()
    {
        AllFetchedLongAgo();
        _fetchedAt["ACB"] = Now.AddHours(-1);    // tươi theo luật của nguồn
        _fetchedAt["BCM"] = Now.AddHours(-1);

        await Sut().WarmNextAsync();

        _warmed.Should().ContainSingle().Which.Should().NotBe("ACB").And.NotBe("BCM");
    }

    [Fact]
    public async Task nothing_is_fetched_when_every_snapshot_is_fresh()
    {
        foreach (var s in Vn30Basket.Symbols) _fetchedAt[s] = Now.AddHours(-1);

        var outcome = await Sut().WarmNextAsync();

        _warmed.Should().BeEmpty();
        outcome.Fetched.Should().Be(0);
    }

    [Fact]
    public async Task warmup_fetches_at_most_symbols_per_run()
    {
        AllFetchedLongAgo();

        await Sut(perRun: 1).WarmNextAsync();
        _warmed.Should().HaveCount(1);

        _warmed.Clear();
        await Sut(perRun: 3).WarmNextAsync();
        _warmed.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(WarmFetchResult.Degraded)]
    [InlineData(WarmFetchResult.BudgetExhausted)]
    [InlineData(WarmFetchResult.Failed)]
    public async Task warmup_stops_when_provider_paused_or_fails(WarmFetchResult result)
    {
        AllFetchedLongAgo();
        _warmer.Setup(w => w.WarmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => { _warmed.Add(s); return result; });

        var outcome = await Sut(perRun: 5).WarmNextAsync();

        _warmed.Should().HaveCount(1, "gặp trục trặc thì dừng lượt này, không thử mã khác");
        outcome.Fetched.Should().Be(0);
    }

    [Fact]
    public async Task a_skipped_symbol_does_not_use_up_the_run()
    {
        AllFetchedLongAgo();
        _fetchedAt.Remove("ACB");
        _warmer.Setup(w => w.WarmAsync("ACB", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => { _warmed.Add(s); return WarmFetchResult.Skipped; });

        var outcome = await Sut(perRun: 1).WarmNextAsync();

        _warmed.Should().HaveCount(2).And.StartWith("ACB");
        outcome.Fetched.Should().Be(1);
    }

    [Fact]
    public async Task warmup_disabled_by_config()
    {
        AllFetchedLongAgo();

        var outcome = await Sut(enabled: false).WarmNextAsync();

        _warmed.Should().BeEmpty();
        _store.Verify(s => s.GetFetchedAtAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        outcome.Fetched.Should().Be(0);
    }

    [Fact]
    public async Task only_vn30_symbols_are_ever_requested_from_the_store()
    {
        AllFetchedLongAgo();

        await Sut().WarmNextAsync();

        _store.Verify(s => s.GetFetchedAtAsync(
            It.Is<IReadOnlyCollection<string>>(c => c.OrderBy(x => x).SequenceEqual(Vn30Basket.Symbols.OrderBy(x => x))),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
