using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Tests.Repositories;

/// <summary>
/// Lệnh đọc Mongo thật không dựng được trong test đơn vị, nên khoá HÌNH DẠNG truy vấn: render bộ lọc ra BSON và kiểm từng điều kiện.
/// Quan trọng nhất là điều kiện UserId của lịch sử tâm trạng (cách ly người dùng).
/// </summary>
public class MoodHistoryFilterTests
{
    private static BsonDocument Render<T>(FilterDefinition<T> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        return filter.Render(new RenderArgs<T>(registry.GetSerializer<T>(), registry));
    }

    [Fact]
    public void MoodRange_filters_by_the_callers_user_id_and_an_inclusive_datekey_range()
    {
        var doc = Render(MoodCheckInRepository.RangeFilter("user-1", "2026-11-01", "2026-11-28"));
        var text = doc.ToJson();

        doc["UserId"].AsString.Should().Be("user-1", "lịch sử chỉ của chính người gọi");
        var range = doc["DateKey"].AsBsonDocument;
        range["$gte"].AsString.Should().Be("2026-11-01");
        range["$lte"].AsString.Should().Be("2026-11-28");
        text.Should().NotContain("$gt\"").And.NotContain("$lt\"");
    }

    [Fact]
    public void TradeRange_filters_by_the_given_portfolios_and_includes_the_whole_last_day()
    {
        var from = new DateTime(2026, 11, 19, 17, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 11, 24, 17, 0, 0, DateTimeKind.Utc);

        var doc = Render(TradeRepository.PortfoliosRangeFilter(new[] { "pf-1", "pf-2" }, from, to));

        doc["PortfolioId"]["$in"].AsBsonArray.Select(v => v.AsString).Should().Equal("pf-1", "pf-2");
        var range = doc["TradeDate"].AsBsonDocument;
        range["$gte"].ToUniversalTime().Should().Be(from, "từ gồm cả mốc đầu");
        range["$lt"].ToUniversalTime().Should().Be(to.AddDays(1), "đến hết ngày cuối: chặn trên là ngày kế tiếp, loại trừ");
    }
}
