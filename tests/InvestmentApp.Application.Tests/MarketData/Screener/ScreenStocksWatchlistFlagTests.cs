using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.ScreenStocks;
using InvestmentApp.Application.MarketData.Screener;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData.Screener;

/// <summary>Cờ inWatchlist trên mỗi dòng đạt: theo watchlist của CHÍNH người gọi, một lần đọc, không đụng tới kho đệm dùng chung.</summary>
public class ScreenStocksWatchlistFlagTests
{
    private static ScreenerRow Row(string symbol) => new(symbol, "HOSE", "8000", 20_000m, 50_000m, 1_000_000m, 50_000m, 12m, 1.5m, 18m, 8m, 500m, 60, 70, 55, 1.1m);

    private readonly Mock<IWatchlistRepository> _watchlists = new();
    private readonly Mock<IMarketScreenerService> _service = new();

    private ScreenStocksQueryHandler Sut(params string[] symbols)
    {
        _service.Setup(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ScreenerSnapshot
        {
            Rows = symbols.Select(Row).ToList(),
            AsOf = DateTimeOffset.UtcNow,
            Hose = new HoseLists(Array.Empty<HoseStatusEntry>(), Array.Empty<HoseDelisting>())
        });
        return new ScreenStocksQueryHandler(_service.Object, _watchlists.Object);
    }

    private void UserHas(string userId, params string[][] lists)
    {
        var made = lists.Select(items =>
        {
            var w = new Watchlist(userId, "L");
            foreach (var s in items) w.AddItem(s);
            return w;
        }).ToList();
        _watchlists.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(made);
    }

    [Fact]
    public async Task rows_in_any_of_the_users_watchlists_are_flagged_and_the_others_are_not()
    {
        var sut = Sut("AAA", "BBB", "CCC");
        UserHas("u1", new[] { "AAA" }, new[] { "CCC", "ZZZ" });

        var result = await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        result.Passed.ToDictionary(p => p.Symbol, p => p.InWatchlist).Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            ["AAA"] = true, ["BBB"] = false, ["CCC"] = true
        });
    }

    [Fact]
    public async Task only_the_callers_watchlists_are_read_in_one_call()
    {
        var sut = Sut("AAA");
        UserHas("u1", new[] { "AAA" });

        await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        _watchlists.Verify(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        _watchlists.Verify(r => r.GetByUserIdAsync(It.Is<string>(u => u != "u1"), It.IsAny<CancellationToken>()), Times.Never);
        _watchlists.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task another_users_watchlist_never_flags_a_row()
    {
        var sut = Sut("AAA");
        UserHas("u1", new[] { "ZZZ" });
        UserHas("u2", new[] { "AAA" });

        var result = await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        result.Passed.Single().InWatchlist.Should().BeFalse();
    }

    [Fact]
    public async Task the_match_ignores_case_so_a_lowercase_stored_symbol_still_flags_its_row()
    {
        var sut = Sut("AAA");
        var w = new Watchlist("u1", "L");
        w.Items.Add(new InvestmentApp.Domain.ValueObjects.WatchlistItem { Symbol = "aaa", AddedAt = DateTime.UtcNow });   // dữ liệu cũ hoặc nhập tay không chuẩn hoá
        _watchlists.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new List<Watchlist> { w });

        var result = await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        result.Passed.Single().InWatchlist.Should().BeTrue();
    }

    [Fact]
    public async Task without_a_user_the_flag_is_false_and_no_watchlist_is_read()
    {
        var sut = Sut("AAA");

        var result = await sut.Handle(new ScreenStocksQuery(), default);

        result.Passed.Single().InWatchlist.Should().BeFalse();
        _watchlists.Verify(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task a_flagged_request_does_not_leak_into_the_next_request_for_someone_else()
    {
        var sut = Sut("AAA");
        UserHas("u1", new[] { "AAA" });

        await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);
        var other = await sut.Handle(new ScreenStocksQuery { UserId = "u2" }, default);

        other.Passed.Single().InWatchlist.Should().BeFalse("cờ nằm trên dòng kết quả của riêng từng lần gọi, không phải trên kho đệm dùng chung");
    }

    [Fact]
    public async Task HOSE_rows_whose_status_could_not_be_verified_are_flagged_too()
    {
        _service.Setup(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ScreenerSnapshot
        {
            Rows = new[] { Row("AAA") },
            AsOf = DateTimeOffset.UtcNow,
            Hose = null   // chưa đọc được danh sách HOSE: mã HOSE vào nhóm "chưa kiểm được"
        });
        var sut = new ScreenStocksQueryHandler(_service.Object, _watchlists.Object);
        UserHas("u1", new[] { "AAA" });

        var result = await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        result.Passed.Should().BeEmpty();
        result.UnverifiedHose.Single().InWatchlist.Should().BeTrue();
    }

    [Fact]
    public async Task nothing_is_flagged_when_the_user_has_no_watchlists()
    {
        var sut = Sut("AAA");
        _watchlists.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new List<Watchlist>());

        var result = await sut.Handle(new ScreenStocksQuery { UserId = "u1" }, default);

        result.Passed.Single().InWatchlist.Should().BeFalse();
    }
}
