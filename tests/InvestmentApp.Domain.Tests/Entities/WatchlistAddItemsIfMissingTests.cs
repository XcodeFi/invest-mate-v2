using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class WatchlistAddItemsIfMissingTests
{
    private static Watchlist Make(params string[] existing)
    {
        var w = new Watchlist("u1", "Theo dõi");
        foreach (var s in existing) w.AddItem(s, "ghi chú cũ");
        return w;
    }

    [Fact]
    public void adds_new_symbols_normalised_and_reports_added_and_skipped()
    {
        var w = Make("FPT");

        var result = w.AddItemsIfMissing(new[] { " vcb ", "FPT", "hpg" }, "Từ sàng lọc 28/11");

        result.Added.Should().Equal("VCB", "HPG");
        result.Skipped.Should().Equal("FPT");
        w.Items.Select(i => i.Symbol).Should().Equal("FPT", "VCB", "HPG");
    }

    [Fact]
    public void the_note_goes_only_on_the_newly_added_items_never_over_an_existing_note()
    {
        var w = Make("FPT");

        w.AddItemsIfMissing(new[] { "FPT", "VCB" }, "Từ sàng lọc");

        w.Items.Single(i => i.Symbol == "FPT").Note.Should().Be("ghi chú cũ");
        w.Items.Single(i => i.Symbol == "VCB").Note.Should().Be("Từ sàng lọc");
    }

    [Fact]
    public void a_symbol_repeated_in_the_input_is_added_once_and_the_repeat_is_skipped()
    {
        var w = Make();

        var result = w.AddItemsIfMissing(new[] { "VCB", "vcb" }, null);

        result.Added.Should().Equal("VCB");
        result.Skipped.Should().Equal("VCB");
        w.Items.Count(i => i.Symbol == "VCB").Should().Be(1);
    }

    [Fact]
    public void version_is_bumped_exactly_once_when_something_was_added()
    {
        var w = Make();
        var before = w.Version;

        w.AddItemsIfMissing(new[] { "VCB", "HPG", "FPT" }, null);

        w.Version.Should().Be(before + 1);
    }

    [Fact]
    public void when_nothing_is_added_the_version_and_updated_at_do_not_move()
    {
        var w = Make("FPT");
        var version = w.Version;
        var updated = w.UpdatedAt;

        var result = w.AddItemsIfMissing(new[] { "FPT" }, null);

        result.Added.Should().BeEmpty();
        w.Version.Should().Be(version);
        w.UpdatedAt.Should().Be(updated);
    }

    [Fact]
    public void blank_entries_are_ignored_not_added_and_not_reported()
    {
        var w = Make();

        var result = w.AddItemsIfMissing(new[] { "", "  ", "VCB" }, null);

        result.Added.Should().Equal("VCB");
        result.Skipped.Should().BeEmpty();
    }
}
