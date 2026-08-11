using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class MoodCheckInTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc);

    private static MoodCheckIn Fomo() => MoodCheckIn.Create("user-1", "2026-08-11", MoodState.Fomo, Now);

    [Fact]
    public void Create_SetsFields_AndLeavesOverrideUnset()
    {
        var checkIn = Fomo();

        checkIn.UserId.Should().Be("user-1");
        checkIn.DateKey.Should().Be("2026-08-11");
        checkIn.Mood.Should().Be(MoodState.Fomo);
        checkIn.CheckedAt.Should().Be(Now);
        checkIn.OverrodeAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RequiresUserId(string? userId)
    {
        var act = () => MoodCheckIn.Create(userId!, "2026-08-11", MoodState.Calm, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-8-1")]
    [InlineData("11/08/2026")]
    public void Create_RequiresDateKeyInIsoDateFormat(string? dateKey)
    {
        var act = () => MoodCheckIn.Create("user-1", dateKey!, MoodState.Calm, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkOverridden_StampsTheTime()
    {
        var checkIn = Fomo();
        var later = Now.AddMinutes(5);

        checkIn.MarkOverridden(later);

        checkIn.OverrodeAt.Should().Be(later);
    }

    [Fact]
    public void MarkOverridden_Twice_KeepsTheFirstStamp()
    {
        var checkIn = Fomo();
        checkIn.MarkOverridden(Now.AddMinutes(5));

        checkIn.MarkOverridden(Now.AddMinutes(30));

        checkIn.OverrodeAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void SetMood_ToADifferentMood_ClearsTheOverride()
    {
        // Không xoá thì chấm Bình tĩnh rồi chấm lại FOMO là mở khoá vĩnh viễn
        // mà chưa lần nào phải bấm qua lớp phủ.
        var checkIn = Fomo();
        checkIn.MarkOverridden(Now.AddMinutes(5));

        checkIn.SetMood(MoodState.Fear, Now.AddHours(1));

        checkIn.Mood.Should().Be(MoodState.Fear);
        checkIn.OverrodeAt.Should().BeNull();
        checkIn.CheckedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void SetMood_ViaCalmAndBack_StillEndsWithNoOverride()
    {
        var checkIn = Fomo();
        checkIn.MarkOverridden(Now.AddMinutes(5));

        checkIn.SetMood(MoodState.Calm, Now.AddHours(1));
        checkIn.SetMood(MoodState.Fomo, Now.AddHours(2));

        checkIn.OverrodeAt.Should().BeNull();
    }

    [Fact]
    public void SetMood_ToTheSameMood_KeepsTheOverride()
    {
        // Chấm lại đúng trạng thái cũ không phải là "đổi" — không bắt bấm qua lớp phủ lần nữa.
        var checkIn = Fomo();
        var overrodeAt = Now.AddMinutes(5);
        checkIn.MarkOverridden(overrodeAt);

        checkIn.SetMood(MoodState.Fomo, Now.AddHours(1));

        checkIn.OverrodeAt.Should().Be(overrodeAt);
    }
}
