using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Repositories;

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
}
