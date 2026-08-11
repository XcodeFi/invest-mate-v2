using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Mood.Commands.MarkMoodOverride;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetTodayMood;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Mood;

public class MoodHandlerTests
{
    private readonly Mock<IMoodCheckInRepository> _repo = new();

    private static string TodayKey => VietnamDate.ToDateKey(DateTime.UtcNow);

    // ─── GetTodayMood ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodayMood_NoCheckInToday_ReturnsNullMood()
    {
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoodCheckIn?)null);

        var result = await new GetTodayMoodQueryHandler(_repo.Object)
            .Handle(new GetTodayMoodQuery { UserId = "user-1" }, CancellationToken.None);

        result.Mood.Should().BeNull();
        result.Overrode.Should().BeFalse();
    }

    [Fact]
    public async Task GetTodayMood_ReadsWithTheCallersOwnUserId_AndTodaysVietnamDateKey()
    {
        _repo.Setup(r => r.GetByUserAndDateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoodCheckIn?)null);

        await new GetTodayMoodQueryHandler(_repo.Object)
            .Handle(new GetTodayMoodQuery { UserId = "user-1" }, CancellationToken.None);

        _repo.Verify(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTodayMood_MapsMoodAndOverrideFlag()
    {
        var checkIn = MoodCheckIn.Create("user-1", TodayKey, MoodState.Fomo, DateTime.UtcNow);
        checkIn.MarkOverridden(DateTime.UtcNow);
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkIn);

        var result = await new GetTodayMoodQueryHandler(_repo.Object)
            .Handle(new GetTodayMoodQuery { UserId = "user-1" }, CancellationToken.None);

        result.Mood.Should().Be(MoodState.Fomo);
        result.Overrode.Should().BeTrue();
    }

    // ─── SetMood ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetMood_FirstTimeToday_CreatesOneRecord()
    {
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoodCheckIn?)null);

        await new SetMoodCommandHandler(_repo.Object)
            .Handle(new SetMoodCommand { UserId = "user-1", Mood = MoodState.Fomo }, CancellationToken.None);

        _repo.Verify(r => r.AddAsync(
            It.Is<MoodCheckIn>(m => m.UserId == "user-1" && m.DateKey == TodayKey && m.Mood == MoodState.Fomo),
            It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetMood_SecondTimeSameDay_UpdatesInsteadOfCreatingASecondRecord()
    {
        var existing = MoodCheckIn.Create("user-1", TodayKey, MoodState.Calm, DateTime.UtcNow);
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await new SetMoodCommandHandler(_repo.Object)
            .Handle(new SetMoodCommand { UserId = "user-1", Mood = MoodState.Revenge }, CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(
            It.Is<MoodCheckIn>(m => m.Mood == MoodState.Revenge), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMood_ChangingMood_ClearsAnEarlierOverride()
    {
        var existing = MoodCheckIn.Create("user-1", TodayKey, MoodState.Fomo, DateTime.UtcNow);
        existing.MarkOverridden(DateTime.UtcNow);
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await new SetMoodCommandHandler(_repo.Object)
            .Handle(new SetMoodCommand { UserId = "user-1", Mood = MoodState.Fear }, CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(
            It.Is<MoodCheckIn>(m => m.OverrodeAt == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMood_TwoConcurrentFirstChecks_LoserFallsBackToUpdate_NotA500()
    {
        // Bấm hai lần liên tiếp: cả hai cùng thấy "chưa có", cả hai cùng insert,
        // cái thứ hai đâm vào unique index. Không bắt thì một thao tác hợp lệ trả 500.
        var winner = MoodCheckIn.Create("user-1", TodayKey, MoodState.Calm, DateTime.UtcNow);
        var lookups = 0;
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => lookups++ == 0 ? null : winner);
        _repo.Setup(r => r.AddAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateMoodCheckInException("user-1", TodayKey));

        var act = async () => await new SetMoodCommandHandler(_repo.Object)
            .Handle(new SetMoodCommand { UserId = "user-1", Mood = MoodState.Fomo }, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _repo.Verify(r => r.UpdateAsync(
            It.Is<MoodCheckIn>(m => m.Mood == MoodState.Fomo), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMood_DuplicateButRowStillNotFound_Rethrows_RatherThanRetryingForever()
    {
        // Trùng index nhưng tìm lại vẫn không ra nghĩa là trùng vì lý do khác — không nuốt.
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoodCheckIn?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateMoodCheckInException("user-1", TodayKey));

        var act = async () => await new SetMoodCommandHandler(_repo.Object)
            .Handle(new SetMoodCommand { UserId = "user-1", Mood = MoodState.Fomo }, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateMoodCheckInException>();
    }

    // ─── MarkMoodOverride ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkOverride_WithoutACheckInToday_WritesNothing_AndReportsFailure()
    {
        // Không được đẻ bản ghi ma cho một tâm trạng người dùng chưa từng chấm.
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoodCheckIn?)null);

        var result = await new MarkMoodOverrideCommandHandler(_repo.Object)
            .Handle(new MarkMoodOverrideCommand { UserId = "user-1" }, CancellationToken.None);

        result.Should().BeFalse();
        _repo.Verify(r => r.AddAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<MoodCheckIn>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkOverride_StampsTodaysCheckIn()
    {
        var existing = MoodCheckIn.Create("user-1", TodayKey, MoodState.Fomo, DateTime.UtcNow);
        _repo.Setup(r => r.GetByUserAndDateAsync("user-1", TodayKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await new MarkMoodOverrideCommandHandler(_repo.Object)
            .Handle(new MarkMoodOverrideCommand { UserId = "user-1" }, CancellationToken.None);

        result.Should().BeTrue();
        _repo.Verify(r => r.UpdateAsync(
            It.Is<MoodCheckIn>(m => m.OverrodeAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }
}
