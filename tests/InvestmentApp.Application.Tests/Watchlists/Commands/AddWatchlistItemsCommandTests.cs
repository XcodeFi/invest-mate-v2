using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItems;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Watchlists.Commands;

public class AddWatchlistItemsCommandTests
{
    private readonly Mock<IWatchlistRepository> _repo = new();

    private AddWatchlistItemsCommandHandler Sut() => new(_repo.Object);

    private Watchlist Existing(string owner = "u1", params string[] symbols)
    {
        var w = new Watchlist(owner, "Từ sàng lọc");
        foreach (var s in symbols) w.AddItem(s);
        _repo.Setup(r => r.GetByIdAsync(w.Id, It.IsAny<CancellationToken>())).ReturnsAsync(w);
        _repo.Setup(r => r.TryUpdateAsync(w, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return w;
    }

    private static AddWatchlistItemsCommand Cmd(Watchlist w, IEnumerable<string> symbols, string? note = null, string user = "u1")
        => new() { WatchlistId = w.Id, UserId = user, Symbols = symbols.ToList(), Note = note };

    [Fact]
    public async Task adds_new_symbols_skips_the_ones_already_there_and_reports_both()
    {
        var w = Existing("u1", "FPT");

        var r = await Sut().Handle(Cmd(w, new[] { "vcb", "FPT", "hpg" }, "Từ sàng lọc 28/11"), default);

        r.Added.Should().Equal("VCB", "HPG");
        r.Skipped.Should().Equal("FPT");
        r.WatchlistId.Should().Be(w.Id);
        w.Items.Select(i => i.Symbol).Should().Equal("FPT", "VCB", "HPG");
    }

    [Fact]
    public async Task another_users_watchlist_is_refused_and_nothing_is_written()
    {
        var w = Existing("owner");

        var act = () => Sut().Handle(Cmd(w, new[] { "VCB" }, user: "intruder"), default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _repo.Verify(r => r.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        w.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task a_missing_watchlist_is_not_found()
    {
        _repo.Setup(r => r.GetByIdAsync("nope", It.IsAny<CancellationToken>())).ReturnsAsync((Watchlist?)null);

        var act = () => Sut().Handle(new AddWatchlistItemsCommand { WatchlistId = "nope", UserId = "u1", Symbols = new() { "VCB" } }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task thirty_distinct_symbols_are_accepted_and_a_thirty_first_is_refused_whole_with_nothing_written()
    {
        var w = Existing();
        var thirty = Enumerable.Range(1, 30).Select(i => $"AA{i}").ToList();

        (await Sut().Handle(Cmd(w, thirty), default)).Added.Should().HaveCount(30);

        var w2 = Existing();
        var act = () => Sut().Handle(Cmd(w2, thirty.Append("AA31")), default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*30*");
        w2.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task repeats_and_case_variants_count_once_towards_the_cap()
    {
        var w = Existing();
        var many = Enumerable.Range(1, 30).Select(i => $"AA{i}").Concat(Enumerable.Range(1, 30).Select(i => $"aa{i}")).ToList();

        var r = await Sut().Handle(Cmd(w, many), default);

        r.Added.Should().HaveCount(30);
        r.Skipped.Should().BeEmpty("trùng ngay trong đầu vào được gộp trước khi đếm và thêm, không phải 'đã có'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("TOOLONGSYMBOL1")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("AB CD")]
    [InlineData("AB-1")]
    [InlineData("<b>")]
    public async Task a_malformed_symbol_refuses_the_whole_call_and_writes_nothing(string bad)
    {
        var w = Existing();

        var act = () => Sut().Handle(Cmd(w, new[] { "VCB", bad }), default);

        await act.Should().ThrowAsync<ArgumentException>();
        _repo.Verify(r => r.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        w.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task a_symbol_of_exactly_ten_characters_is_accepted()
    {
        var w = Existing();

        var r = await Sut().Handle(Cmd(w, new[] { "ABCDEFGHIJ" }), default);

        r.Added.Should().Equal("ABCDEFGHIJ");
    }

    [Fact]
    public async Task a_note_of_exactly_200_characters_is_accepted()
    {
        var w = Existing();

        var r = await Sut().Handle(Cmd(w, new[] { "VCB" }, new string('x', 200)), default);

        r.Added.Should().Equal("VCB");
        w.Items.Single().Note.Should().HaveLength(200);
    }

    [Fact]
    public async Task the_note_is_trimmed_before_it_is_measured_and_stored()
    {
        var w = Existing();
        await Sut().Handle(Cmd(w, new[] { "VCB" }, "  Từ sàng lọc  "), default);
        w.Items.Single().Note.Should().Be("Từ sàng lọc");

        var w2 = Existing();
        var padded = "  " + new string('x', 200) + "  ";
        var r = await Sut().Handle(Cmd(w2, new[] { "FPT" }, padded), default);
        r.Added.Should().Equal(new[] { "FPT" }, "200 ký tự thật sau khi cắt khoảng trắng thì đạt");
        w2.Items.Single().Note.Should().HaveLength(200);
    }

    [Fact]
    public async Task ownership_is_checked_again_on_the_retry_read_and_a_foreign_watchlist_gets_no_write()
    {
        var mine = new Watchlist("u1", "L");
        var id = mine.Id;
        var foreign = new Watchlist("someone-else", "L");
        typeof(Watchlist).BaseType!.GetProperty("Id")!.SetValue(foreign, id);
        _repo.SetupSequence(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(mine).ReturnsAsync(foreign);
        _repo.Setup(r => r.TryUpdateAsync(mine, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => Sut().Handle(new AddWatchlistItemsCommand { WatchlistId = id, UserId = "u1", Symbols = new() { "VCB" } }, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _repo.Verify(r => r.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        foreign.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task an_empty_symbol_list_is_refused()
    {
        var w = Existing();

        var act = () => Sut().Handle(Cmd(w, Array.Empty<string>()), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task a_note_longer_than_200_characters_is_refused()
    {
        var w = Existing();

        var act = () => Sut().Handle(Cmd(w, new[] { "VCB" }, new string('x', 201)), default);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public async Task when_nothing_new_is_added_nothing_is_written()
    {
        var w = Existing("u1", "FPT");

        var r = await Sut().Handle(Cmd(w, new[] { "FPT" }), default);

        r.Added.Should().BeEmpty();
        r.Skipped.Should().Equal("FPT");
        _repo.Verify(x => x.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task the_write_is_conditional_on_the_version_that_was_read()
    {
        var w = Existing();
        var readVersion = w.Version;

        await Sut().Handle(Cmd(w, new[] { "VCB" }), default);

        _repo.Verify(r => r.TryUpdateAsync(w, readVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task a_concurrent_write_is_retried_on_a_fresh_copy_and_succeeds()
    {
        var first = new Watchlist("u1", "L");
        var fresh = new Watchlist("u1", "L");
        var id = first.Id;
        typeof(Watchlist).BaseType!.GetProperty("Id")!.SetValue(fresh, id);
        _repo.SetupSequence(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(first).ReturnsAsync(fresh);
        _repo.Setup(r => r.TryUpdateAsync(first, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.TryUpdateAsync(fresh, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var r = await Sut().Handle(new AddWatchlistItemsCommand { WatchlistId = id, UserId = "u1", Symbols = new() { "VCB" } }, default);

        r.Added.Should().Equal("VCB");
        fresh.Items.Select(i => i.Symbol).Should().Contain("VCB");
    }

    [Fact]
    public async Task persistent_conflicts_end_in_a_clear_error_not_a_silent_loss()
    {
        var w = Existing();
        // Mỗi lần đọc lại là một bản mới từ kho (chưa có VCB), như thật; đọc lại cùng một đối tượng đã sửa sẽ thành "không có gì mới".
        _repo.Setup(r => r.GetByIdAsync(w.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            var copy = new Watchlist("u1", "Từ sàng lọc");
            typeof(Watchlist).BaseType!.GetProperty("Id")!.SetValue(copy, w.Id);
            return copy;
        });
        _repo.Setup(r => r.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => Sut().Handle(Cmd(w, new[] { "VCB" }), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*thử lại*");
        _repo.Verify(r => r.TryUpdateAsync(It.IsAny<Watchlist>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
