using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>Lịch sử đổi trong ngày (<see cref="MoodCheckIn.Changes"/>): mỗi lần đổi một mục có nguồn, tối đa 20, lặp lại đúng trạng thái cũ thì bỏ qua.</summary>
public class MoodCheckInChangesTests
{
    private static readonly DateTime T0 = new(2026, 11, 28, 3, 0, 0, DateTimeKind.Utc);

    private static MoodCheckIn Legacy(MoodState mood)
    {
        var legacy = new MoodCheckIn();   // tài liệu cũ: không có Changes
        typeof(MoodCheckIn).GetProperty(nameof(MoodCheckIn.Mood))!.SetValue(legacy, mood);
        return legacy;
    }

    [Fact]
    public void Create_RecordsTheFirstChangeWithItsSource_WebByDefault()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, T0);

        c.Changes.Should().ContainSingle();
        c.Changes[0].Should().Be(new MoodChange(T0, MoodState.Calm, "web"));
    }

    [Fact]
    public void Create_RecordsTheGivenSource()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Fomo, T0, MoodChange.SourceMcp);

        c.Changes[0].Source.Should().Be("mcp");
    }

    [Fact]
    public void SetMood_AppendsAChange_WhenTheMoodDiffers()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, T0);

        c.SetMood(MoodState.Fomo, T0.AddHours(2));

        c.Changes.Select(x => x.Mood).Should().Equal(MoodState.Calm, MoodState.Fomo);
        c.Changes[1].At.Should().Be(T0.AddHours(2));
        c.Mood.Should().Be(MoodState.Fomo);
    }

    [Fact]
    public void SetMood_SkipsARepeatOfTheSameMood_ButStillRefreshesCheckedAt()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Fomo, T0);

        c.SetMood(MoodState.Fomo, T0.AddHours(1));

        c.Changes.Should().ContainSingle();
        c.CheckedAt.Should().Be(T0.AddHours(1));
    }

    [Fact]
    public void SetMood_RecordsTheSourcePerEntry()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, T0);

        c.SetMood(MoodState.Fear, T0.AddHours(1), MoodChange.SourceMcp);

        c.Changes.Select(x => x.Source).Should().Equal("web", "mcp");
    }

    [Fact]
    public void Changes_AreCappedAt20_DroppingTheOldest_AndMoodStaysTheTruth()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, T0);
        for (var i = 1; i <= 30; i++)
            c.SetMood(i % 2 == 1 ? MoodState.Fomo : MoodState.Calm, T0.AddMinutes(i));

        c.Changes.Should().HaveCount(MoodCheckIn.MaxChanges);
        c.Changes.Last().At.Should().Be(T0.AddMinutes(30));
        c.Changes.First().At.Should().Be(T0.AddMinutes(11), "bản ghi cũ nhất bị bỏ trước");
        c.Mood.Should().Be(c.Changes.Last().Mood);
    }

    [Fact]
    public void ADocumentWithoutChanges_StillWorks_AndTheFirstRealChangeIsRecorded()
    {
        var legacy = Legacy(MoodState.Calm);

        legacy.Changes.Should().BeEmpty();
        legacy.SetMood(MoodState.Fomo, T0);

        legacy.Changes.Should().ContainSingle();
        legacy.Changes[0].Should().Be(new MoodChange(T0, MoodState.Fomo, "web"));
    }

    [Fact]
    public void ARepeatOnALegacyDocument_IsAlsoSkipped()
    {
        var legacy = Legacy(MoodState.Fomo);

        legacy.SetMood(MoodState.Fomo, T0);

        legacy.Changes.Should().BeEmpty();
    }

    [Fact]
    public void LatestSource_IsTheSourceOfTheLastChange()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, T0);
        c.LatestSource.Should().Be("web");

        c.SetMood(MoodState.Fomo, T0.AddHours(1), MoodChange.SourceMcp);

        c.LatestSource.Should().Be("mcp");
    }

    [Fact]
    public void AWebChange_StillClearsTheOverrideStamp()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Fomo, T0);
        c.MarkOverridden(T0.AddMinutes(5));

        c.SetMood(MoodState.Calm, T0.AddHours(1));

        c.OverrodeAt.Should().BeNull();
    }

    [Fact]
    public void AnMcpChange_NeverTouchesTheOverrideStamp_InEitherDirection()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Fomo, T0);
        c.MarkOverridden(T0.AddMinutes(5));

        c.SetMood(MoodState.Fear, T0.AddHours(1), MoodChange.SourceMcp);

        c.OverrodeAt.Should().Be(T0.AddMinutes(5), "agent không được xoá dấu đã bấm qua lớp phủ");

        var d = MoodCheckIn.Create("u", "2026-11-29", MoodState.Calm, T0);
        d.SetMood(MoodState.Fomo, T0.AddHours(1), MoodChange.SourceMcp);
        d.OverrodeAt.Should().BeNull("agent cũng không được tự đặt dấu bỏ qua");
    }
}

/// <summary>Đọc/ghi BSON thật: bản ghi mới giữ nguyên Changes, bản ghi cũ (không có trường Changes) vẫn đọc được.</summary>
public class MoodCheckInBsonTests
{
    [Fact]
    public void RoundTrip_KeepsTheChangesAndTheirSources()
    {
        var c = MoodCheckIn.Create("u", "2026-11-28", MoodState.Calm, new DateTime(2026, 11, 28, 3, 0, 0, DateTimeKind.Utc));
        c.SetMood(MoodState.Fomo, new DateTime(2026, 11, 28, 5, 0, 0, DateTimeKind.Utc), MoodChange.SourceMcp);

        var bson = MongoDB.Bson.BsonExtensionMethods.ToBson(c);
        var back = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MoodCheckIn>(bson);

        back.Changes.Select(x => (x.Mood, x.Source)).Should().Equal((MoodState.Calm, "web"), (MoodState.Fomo, "mcp"));
        back.LatestSource.Should().Be("mcp");
        back.Mood.Should().Be(MoodState.Fomo);
    }

    [Fact]
    public void A_legacy_document_without_a_Changes_field_deserializes_with_an_empty_list()
    {
        var legacy = new MongoDB.Bson.BsonDocument
        {
            { "_id", "m1" }, { "UserId", "u" }, { "DateKey", "2026-08-11" }, { "Mood", 1 },
            { "CheckedAt", new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc) }
        };

        var back = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MoodCheckIn>(legacy);

        back.Mood.Should().Be(MoodState.Fomo);
        back.Changes.Should().BeEmpty();
        back.LatestSource.Should().BeNull();
    }
}
