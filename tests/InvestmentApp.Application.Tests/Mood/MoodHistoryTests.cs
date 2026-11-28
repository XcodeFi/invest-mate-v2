using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetMoodHistory;
using InvestmentApp.Application.Mood.Queries.GetTodayMood;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Mood;

public class MoodHistoryTests
{
    private readonly Mock<IMoodCheckInRepository> _moods = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ITradeRepository> _trades = new();

    // 2026-11-28 10:00 giờ VN = 03:00 UTC
    private static readonly DateTime NowUtc = new(2026, 11, 28, 3, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private GetMoodHistoryQueryHandler Sut() => new(_moods.Object, _portfolios.Object, _trades.Object, new FixedTime(NowUtc));

    private void Portfolio(string userId, string id)
    {
        var p = new Portfolio(userId, "P", 1_000_000m);
        typeof(AggregateRoot).GetProperty("Id")!.SetValue(p, id);
        _portfolios.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio> { p });
    }

    private void Moods(string userId, params MoodCheckIn[] days)
        => _moods.Setup(r => r.GetByUserAndDateRangeAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(days.ToList());

    private void Trades(params Trade[] trades)
        => _trades.Setup(r => r.GetByPortfolioIdsAndDateRangeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades.ToList());

    private static Trade TradeAt(DateTime utc) => new("pf-1", "FPT", TradeType.BUY, 100, 100_000m, 0, 0, utc);

    private static MoodCheckIn CheckIn(string userId, string dateKey, MoodState mood)
        => MoodCheckIn.Create(userId, dateKey, mood, DateTime.SpecifyKind(DateTime.Parse(dateKey), DateTimeKind.Utc).AddHours(2));

    // ───── khoảng ngày ─────

    [Fact]
    public async Task Default_range_is_the_last_90_vn_days_ending_today()
    {
        Portfolio("u1", "pf-1"); Moods("u1"); Trades();

        var result = await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default);

        result.To.Should().Be("2026-11-28");
        result.From.Should().Be("2026-08-31");   // 90 ngày gồm cả hôm nay: 2026-11-28 trừ 89 ngày
        _moods.Verify(r => r.GetByUserAndDateRangeAsync("u1", "2026-08-31", "2026-11-28", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Range_longer_than_366_days_is_rejected()
    {
        Portfolio("u1", "pf-1"); Moods("u1"); Trades();

        var act = () => Sut().Handle(new GetMoodHistoryQuery { UserId = "u1", From = new DateTime(2025, 1, 1), To = new DateTime(2026, 11, 28) }, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Range_of_exactly_366_days_is_accepted()
    {
        Portfolio("u1", "pf-1"); Moods("u1"); Trades();

        var result = await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1", From = new DateTime(2025, 11, 28), To = new DateTime(2026, 11, 28) }, default);

        result.From.Should().Be("2025-11-28");
    }

    [Fact]
    public async Task From_after_to_is_rejected()
    {
        var act = () => Sut().Handle(new GetMoodHistoryQuery { UserId = "u1", From = new DateTime(2026, 11, 28), To = new DateTime(2026, 11, 1) }, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ───── cách ly người dùng ─────

    [Fact]
    public async Task Another_users_days_never_appear_the_query_is_scoped_to_the_callers_id()
    {
        Portfolio("u1", "pf-1");
        Moods("u1", CheckIn("u1", "2026-11-27", MoodState.Calm));
        Trades();

        var result = await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default);

        result.Days.Select(d => d.DateKey).Should().Equal("2026-11-27");
        _moods.Verify(r => r.GetByUserAndDateRangeAsync(It.Is<string>(u => u != "u1"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _portfolios.Verify(r => r.GetByUserIdAsync(It.Is<string>(u => u != "u1"), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ───── ngày VN của lệnh ─────

    [Fact]
    public async Task A_trade_at_23_30_vn_and_one_at_00_30_vn_land_on_different_days()
    {
        Portfolio("u1", "pf-1");
        Moods("u1", CheckIn("u1", "2026-11-26", MoodState.Fomo), CheckIn("u1", "2026-11-27", MoodState.Calm));
        // 23:30 VN ngày 26/11 = 16:30 UTC ngày 26; 00:30 VN ngày 27/11 = 17:30 UTC ngày 26
        Trades(TradeAt(new DateTime(2026, 11, 26, 16, 30, 0, DateTimeKind.Utc)), TradeAt(new DateTime(2026, 11, 26, 17, 30, 0, DateTimeKind.Utc)));

        var result = await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default);

        var d26 = result.Days.Single(d => d.DateKey == "2026-11-26");
        var d27 = result.Days.Single(d => d.DateKey == "2026-11-27");
        d26.TradeCount.Should().Be(1);
        d27.TradeCount.Should().Be(1, "00:30 giờ VN là ngày 27, dù theo UTC vẫn là ngày 26");
        d26.EmotionalTrade.Should().BeTrue("ngày 26 chấm FOMO và có lệnh");
        d27.EmotionalTrade.Should().BeFalse("ngày 27 chấm Bình tĩnh");
    }

    [Fact]
    public async Task Trades_are_loaded_in_one_query_for_the_whole_range_not_per_day()
    {
        Portfolio("u1", "pf-1"); Moods("u1"); Trades();

        await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default);

        _trades.Verify(r => r.GetByPortfolioIdsAndDateRangeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_trade_on_a_day_with_no_checkin_is_unrated_never_emotional()
    {
        Portfolio("u1", "pf-1"); Moods("u1");
        Trades(TradeAt(new DateTime(2026, 11, 25, 5, 0, 0, DateTimeKind.Utc)));

        var day = (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single();

        day.DateKey.Should().Be("2026-11-25");
        day.Mood.Should().BeNull();
        day.TradeCount.Should().Be(1);
        day.EmotionalTrade.Should().BeFalse();
    }

    // ───── lệnh khi đang cảm xúc ─────

    [Fact]
    public async Task Trade_on_a_day_that_was_ever_not_calm_is_emotional_even_if_it_ended_calm()
    {
        var c = CheckIn("u1", "2026-11-27", MoodState.Fomo);
        c.SetMood(MoodState.Calm, DateTime.SpecifyKind(DateTime.Parse("2026-11-27"), DateTimeKind.Utc).AddHours(4));
        Portfolio("u1", "pf-1"); Moods("u1", c);
        Trades(TradeAt(new DateTime(2026, 11, 27, 5, 0, 0, DateTimeKind.Utc)));

        var day = (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single();

        day.Mood.Should().Be(MoodState.Calm);
        day.EmotionalTrade.Should().BeTrue("trong ngày từng chấm FOMO");
    }

    [Fact]
    public async Task A_non_calm_day_without_any_trade_is_not_an_emotional_trade()
    {
        Portfolio("u1", "pf-1");
        Moods("u1", CheckIn("u1", "2026-11-27", MoodState.Fear));
        Trades();

        var day = (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single();

        day.Mood.Should().Be(MoodState.Fear);
        day.TradeCount.Should().Be(0);
        day.EmotionalTrade.Should().BeFalse("không có lệnh nào thì không có 'đặt lệnh khi đang cảm xúc'");
    }

    [Fact]
    public async Task A_calm_day_with_trades_is_not_emotional()
    {
        Portfolio("u1", "pf-1");
        Moods("u1", CheckIn("u1", "2026-11-27", MoodState.Calm));
        Trades(TradeAt(new DateTime(2026, 11, 27, 5, 0, 0, DateTimeKind.Utc)));

        (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single().EmotionalTrade.Should().BeFalse();
    }

    [Fact]
    public async Task An_overridden_day_is_emotional()
    {
        var c = CheckIn("u1", "2026-11-27", MoodState.Fomo);
        c.MarkOverridden(DateTime.SpecifyKind(DateTime.Parse("2026-11-27"), DateTimeKind.Utc).AddHours(3));
        Portfolio("u1", "pf-1"); Moods("u1", c); Trades();

        var day = (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single();

        day.Overrode.Should().BeTrue();
        day.EmotionalTrade.Should().BeTrue();
    }

    [Fact]
    public async Task Each_day_carries_its_changes_with_their_sources()
    {
        var c = CheckIn("u1", "2026-11-27", MoodState.Calm);
        c.SetMood(MoodState.Fear, DateTime.SpecifyKind(DateTime.Parse("2026-11-27"), DateTimeKind.Utc).AddHours(5), MoodChange.SourceMcp);
        Portfolio("u1", "pf-1"); Moods("u1", c); Trades();

        var day = (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Single();

        day.Changes.Select(x => (x.Mood, x.Source)).Should().Equal((MoodState.Calm, "web"), (MoodState.Fear, "mcp"));
    }

    [Fact]
    public async Task Days_are_sorted_by_date_ascending()
    {
        Portfolio("u1", "pf-1");
        Moods("u1", CheckIn("u1", "2026-11-27", MoodState.Calm), CheckIn("u1", "2026-11-20", MoodState.Calm));
        Trades(TradeAt(new DateTime(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc)));

        (await Sut().Handle(new GetMoodHistoryQuery { UserId = "u1" }, default)).Days.Select(d => d.DateKey)
            .Should().Equal("2026-11-20", "2026-11-23", "2026-11-27");
    }

    // ───── ngưỡng tự đánh giá: Calm từ agent không mở khoá ─────

    [Fact]
    public async Task GetToday_an_mcp_calm_does_not_count_as_checked_and_says_it_came_from_the_agent()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        var c = MoodCheckIn.Create("u1", key, MoodState.Calm, DateTime.UtcNow, MoodChange.SourceMcp);
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>())).ReturnsAsync(c);

        var result = await new GetTodayMoodQueryHandler(_moods.Object).Handle(new GetTodayMoodQuery { UserId = "u1" }, default);

        result.Mood.Should().BeNull("Bình tĩnh do agent ghi không thay lời tự chấm của người dùng");
        result.FromAgent.Should().BeTrue();
    }

    [Fact]
    public async Task GetToday_an_mcp_emotional_mood_does_apply_that_is_the_safe_direction()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        var c = MoodCheckIn.Create("u1", key, MoodState.Fomo, DateTime.UtcNow, MoodChange.SourceMcp);
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>())).ReturnsAsync(c);

        var result = await new GetTodayMoodQueryHandler(_moods.Object).Handle(new GetTodayMoodQuery { UserId = "u1" }, default);

        result.Mood.Should().Be(MoodState.Fomo);
        result.FromAgent.Should().BeTrue();
    }

    [Fact]
    public async Task GetToday_a_web_calm_after_an_mcp_change_counts_as_checked()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        var c = MoodCheckIn.Create("u1", key, MoodState.Fomo, DateTime.UtcNow, MoodChange.SourceMcp);
        c.SetMood(MoodState.Calm, DateTime.UtcNow.AddMinutes(1));   // chính người dùng chấm lại
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>())).ReturnsAsync(c);

        var result = await new GetTodayMoodQueryHandler(_moods.Object).Handle(new GetTodayMoodQuery { UserId = "u1" }, default);

        result.Mood.Should().Be(MoodState.Calm);
        result.FromAgent.Should().BeFalse();
    }

    [Fact]
    public async Task GetToday_a_web_calm_is_unchanged()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MoodCheckIn.Create("u1", key, MoodState.Calm, DateTime.UtcNow));

        var result = await new GetTodayMoodQueryHandler(_moods.Object).Handle(new GetTodayMoodQuery { UserId = "u1" }, default);

        result.Mood.Should().Be(MoodState.Calm);
        result.FromAgent.Should().BeFalse();
    }

    // ───── SetMood từ MCP ─────

    [Fact]
    public async Task SetMood_with_mcp_source_records_it_and_leaves_OverrodeAt_alone()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        var existing = MoodCheckIn.Create("u1", key, MoodState.Fomo, DateTime.UtcNow.AddHours(-1));
        existing.MarkOverridden(DateTime.UtcNow.AddMinutes(-30));
        var stamp = existing.OverrodeAt;
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await new SetMoodCommandHandler(_moods.Object).Handle(new SetMoodCommand { UserId = "u1", Mood = MoodState.Calm, Source = MoodChange.SourceMcp }, default);

        existing.LatestSource.Should().Be("mcp");
        existing.OverrodeAt.Should().Be(stamp);
        _moods.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMood_defaults_to_the_web_source()
    {
        var key = VietnamDate.ToDateKey(DateTime.UtcNow);
        _moods.Setup(r => r.GetByUserAndDateAsync("u1", key, It.IsAny<CancellationToken>())).ReturnsAsync((MoodCheckIn?)null);
        MoodCheckIn? saved = null;
        _moods.Setup(r => r.AddAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>())).Callback<MoodCheckIn, CancellationToken>((m, _) => saved = m).Returns(Task.CompletedTask);

        await new SetMoodCommandHandler(_moods.Object).Handle(new SetMoodCommand { UserId = "u1", Mood = MoodState.Calm }, default);

        saved!.LatestSource.Should().Be("web");
    }
}
