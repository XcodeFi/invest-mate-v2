using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace InvestmentApp.Infrastructure.Tests.Repositories;

/// <summary>
/// Phần ánh xạ giữa chuỗi trong bộ nhớ và bản ghi Mongo. Lệnh đọc/ghi Mongo thật nằm ở lớp keo mỏng của
/// <see cref="AdjustedPriceSeriesStore"/> và không dựng được trong test đơn vị (driver không giả được), nên chỉ phần này được khoá.
/// </summary>
public class AdjustedPriceSeriesStoreMappingTests
{
    private static readonly DateTime Fetched = new(2026, 11, 28, 4, 30, 0, DateTimeKind.Utc);

    private static List<StockPriceData> Bars() => new()
    {
        new() { Symbol = "FPT", Date = new DateTime(2026, 9, 21), Open = 65_003m, High = 65_297m, Low = 63_537m, Close = 64_906m, Volume = 8_488_671, IsAdjusted = true },
        new() { Symbol = "FPT", Date = new DateTime(2026, 9, 18), Open = 66_204m, High = 66_470m, Low = 63_715m, Close = 63_715m, Volume = 15_529_012, IsAdjusted = true },
    };

    [Fact]
    public void Document_IdIsTheSymbol_SoOneRecordPerSymbolAndNoExtraIndexIsNeeded()
    {
        var doc = AdjustedPriceSeriesStore.ToDocument("FPT", Fetched, Bars());

        doc.Id.Should().Be("FPT");
        doc.Symbol.Should().Be("FPT");
        doc.FetchedAt.Should().Be(Fetched);
        doc.FetchedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void RoundTrip_KeepsEveryFieldAndRestoresAscendingDates()
    {
        var snapshot = AdjustedPriceSeriesStore.ToSnapshot(AdjustedPriceSeriesStore.ToDocument("FPT", Fetched, Bars()));

        snapshot.Symbol.Should().Be("FPT");
        snapshot.FetchedAtUtc.Should().Be(Fetched);
        snapshot.Bars.Select(b => b.Date).Should().BeInAscendingOrder();
        var b = snapshot.Bars.Single(x => x.Date == new DateTime(2026, 9, 18));
        (b.Open, b.High, b.Low, b.Close, b.Volume).Should().Be((66_204m, 66_470m, 63_715m, 63_715m, 15_529_012L));
    }

    [Fact]
    public void RestoredBars_AreFlaggedAdjusted_BecauseOnlyAdjustedSeriesAreStored()
    {
        var snapshot = AdjustedPriceSeriesStore.ToSnapshot(AdjustedPriceSeriesStore.ToDocument("FPT", Fetched, Bars()));

        snapshot.Bars.Should().OnlyContain(b => b.IsAdjusted && b.Symbol == "FPT");
    }

    [Fact]
    public void CollectionIsSeparateFromRawStockPrices()
    {
        AdjustedPriceSeriesStore.CollectionName.Should().Be("adjusted_price_series").And.NotBe("stock_prices");
    }

    // ───────────── Vòng qua BSON thật (không cần DB): kiểu số và ngày phải sống sót đúng từng chữ số ─────────────

    private static AdjustedPriceSeriesDocument ThroughBson(AdjustedPriceSeriesDocument doc)
    {
        var bytes = doc.ToBson();
        return BsonSerializer.Deserialize<AdjustedPriceSeriesDocument>(bytes);
    }

    [Fact]
    public void Bson_RoundTrip_KeepsDecimalsDatesAndVolumeExactly()
    {
        var bars = new List<StockPriceData>
        {
            new() { Symbol = "FPT", Date = new DateTime(2026, 9, 18), Open = 55_398.123456789m, High = 66_470.5m, Low = 0.001m, Close = 63_715m, Volume = 15_529_012_345L, IsAdjusted = true },
            new() { Symbol = "FPT", Date = new DateTime(2017, 1, 3), Open = 12_345.678m, High = 12_400m, Low = 12_300m, Close = 12_345.678m, Volume = 0, IsAdjusted = true },
        };

        var restored = AdjustedPriceSeriesStore.ToSnapshot(ThroughBson(AdjustedPriceSeriesStore.ToDocument("FPT", Fetched, bars)));

        restored.Symbol.Should().Be("FPT");
        restored.FetchedAtUtc.Should().Be(Fetched);
        restored.FetchedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        var b = restored.Bars.Single(x => x.Date == new DateTime(2026, 9, 18));
        b.Open.Should().Be(55_398.123456789m);
        b.Low.Should().Be(0.001m);
        b.Volume.Should().Be(15_529_012_345L, "khối lượng vượt int32");
        restored.Bars.Single(x => x.Date == new DateTime(2017, 1, 3)).Close.Should().Be(12_345.678m);
        restored.Bars.Select(x => x.Date).Should().BeInAscendingOrder().And.OnlyContain(d => d.TimeOfDay == TimeSpan.Zero);
    }

    [Fact]
    public void Bson_Document_IsStoredUnderTheSymbolAsId_AndASeriesOfThreeYearsStaysSmall()
    {
        var start = new DateTime(2023, 12, 1);
        var bars = Enumerable.Range(0, 800).Select(i => new StockPriceData
        {
            Symbol = "FPT", Date = start.AddDays(i), Open = 60_000m + i, High = 61_000m + i, Low = 59_000m + i, Close = 60_500m + i, Volume = 1_000_000 + i, IsAdjusted = true
        }).ToList();

        var doc = AdjustedPriceSeriesStore.ToDocument("FPT", Fetched, bars);
        var bson = doc.ToBsonDocument();

        bson["_id"].AsString.Should().Be("FPT");
        doc.ToBson().Length.Should().BeLessThan(1_000_000, "xa dưới giới hạn 16MB của một document Mongo");
        ThroughBson(doc).Bars.Should().HaveCount(800);
    }
}
