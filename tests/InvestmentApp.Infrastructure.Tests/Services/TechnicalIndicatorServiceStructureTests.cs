using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// SMA50/150/200, kênh Donchian (20/55 và đáy 10 phiên) và đỉnh/đáy 52 tuần: chuỗi theo phiên và giá trị scalar phải khớp nhau,
/// thiếu dữ liệu là null (không 0), và thẻ Trend Template đi cùng phân tích.
/// </summary>
public class TechnicalIndicatorServiceStructureTests
{
    private const string Symbol = "FPT";
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly TechnicalIndicatorService _sut;

    public TechnicalIndicatorServiceStructureTests() => _sut = new TechnicalIndicatorService(_marketData.Object);

    private static List<StockPriceData> Prices(int days, int seed = 7)
    {
        var rng = new Random(seed);
        var list = new List<StockPriceData>();
        var close = 80_000m;
        var date = DateTime.UtcNow.Date.AddDays(-days);
        for (int i = 0; i < days; i++)
        {
            var open = close;
            close = Math.Max(1_000m, close + rng.Next(-1_500, 1_600));
            list.Add(new StockPriceData
            {
                Symbol = Symbol, Date = date.AddDays(i), Open = open,
                High = Math.Max(open, close) + rng.Next(0, 900), Low = Math.Min(open, close) - rng.Next(0, 900), Close = close,
                Volume = 1_000_000 + rng.Next(0, 800_000)
            });
        }
        return list;
    }

    private void Setup(List<StockPriceData> prices) =>
        _marketData.Setup(m => m.GetHistoricalPricesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

    [Fact]
    public async Task Sma_series_are_simple_averages_of_the_closes_and_null_until_the_window_fills()
    {
        var prices = Prices(400);
        Setup(prices);

        var s = await _sut.GetSeriesAsync(Symbol, 36);

        s.Sma50.Should().HaveCount(400);
        s.Sma50[48].Should().BeNull();
        s.Sma50[49].Should().Be(prices.Take(50).Average(p => p.Close));
        s.Sma150[148].Should().BeNull();
        s.Sma200[198].Should().BeNull();
        s.Sma200[^1].Should().Be(prices.Skip(200).Average(p => p.Close));
    }

    [Fact]
    public async Task Donchian_channel_uses_the_prior_sessions_and_excludes_the_current_one()
    {
        var prices = Prices(400);
        Setup(prices);

        var s = await _sut.GetSeriesAsync(Symbol, 36);

        var i = 399;
        s.Donchian20High[i].Should().Be(prices.Skip(i - 20).Take(20).Max(p => p.High));
        s.Donchian20Low[i].Should().Be(prices.Skip(i - 20).Take(20).Min(p => p.Low));
        s.Donchian55High[i].Should().Be(prices.Skip(i - 55).Take(55).Max(p => p.High));
        s.Donchian55Low[i].Should().Be(prices.Skip(i - 55).Take(55).Min(p => p.Low));
        s.ExitLow10[i].Should().Be(prices.Skip(i - 10).Take(10).Min(p => p.Low));
        s.Donchian20High[19].Should().BeNull();
        s.Donchian20High[20].Should().NotBeNull();
        s.Donchian55High[54].Should().BeNull();
        s.ExitLow10[9].Should().BeNull();
    }

    [Fact]
    public async Task The_new_series_have_the_same_length_as_the_dates_also_after_the_window_is_cut()
    {
        Setup(Prices(400));

        var s = await _sut.GetSeriesAsync(Symbol, 3);   // chỉ trả khoảng 3 tháng cuối

        s.Dates.Count.Should().BeLessThan(400);
        foreach (var series in new[] { s.Sma50, s.Sma150, s.Sma200, s.Donchian20High, s.Donchian20Low, s.Donchian55High, s.Donchian55Low, s.ExitLow10 })
            series.Should().HaveCount(s.Dates.Count);
        s.Sma200[^1].Should().NotBeNull();   // đã tính trên toàn lịch sử nên cuối khoảng vẫn có giá trị
    }

    [Fact]
    public async Task Under_20_sessions_every_new_series_is_null_not_zero()
    {
        Setup(Prices(10));

        var s = await _sut.GetSeriesAsync(Symbol, 12);

        foreach (var series in new[] { s.Sma50, s.Sma150, s.Sma200, s.Donchian20High, s.Donchian20Low, s.Donchian55High, s.Donchian55Low, s.ExitLow10 })
        {
            series.Should().HaveCount(10);
            series.Should().OnlyContain(v => v == null);
        }
    }

    [Fact]
    public async Task Analysis_scalars_equal_the_last_point_of_the_series()
    {
        Setup(Prices(400));

        var a = await _sut.AnalyzeAsync(Symbol);
        var s = await _sut.GetSeriesAsync(Symbol, 36);

        a.Sma50.Should().Be(s.Sma50[^1]);
        a.Sma150.Should().Be(s.Sma150[^1]);
        a.Sma200.Should().Be(s.Sma200[^1]);
        a.Donchian20High.Should().Be(s.Donchian20High[^1]);
        a.Donchian20Low.Should().Be(s.Donchian20Low[^1]);
        a.Donchian55High.Should().Be(s.Donchian55High[^1]);
        a.Donchian55Low.Should().Be(s.Donchian55Low[^1]);
        a.ExitLow10.Should().Be(s.ExitLow10[^1]);
    }

    [Fact]
    public async Task Week52_high_and_low_come_from_the_last_250_sessions_with_the_distance_in_percent()
    {
        var prices = Prices(400);
        Setup(prices);

        var a = await _sut.AnalyzeAsync(Symbol);

        var last250 = prices.Skip(150).ToList();
        a.Week52High.Should().Be(last250.Max(p => p.High));
        a.Week52Low.Should().Be(last250.Min(p => p.Low));
        var price = prices[^1].Close;
        a.PctBelowWeek52High.Should().Be(Math.Round((a.Week52High!.Value - price) / a.Week52High.Value * 100, 2));
        a.PctAboveWeek52Low.Should().Be(Math.Round((price / a.Week52Low!.Value - 1) * 100, 2));
    }

    [Fact]
    public async Task With_fewer_than_250_sessions_the_52_week_fields_are_null_and_the_sma200_still_exists()
    {
        Setup(Prices(230));

        var a = await _sut.AnalyzeAsync(Symbol);

        a.Week52High.Should().BeNull();
        a.Week52Low.Should().BeNull();
        a.PctBelowWeek52High.Should().BeNull();
        a.PctAboveWeek52Low.Should().BeNull();
        a.Sma200.Should().NotBeNull();
        a.Sma150.Should().NotBeNull();
    }

    [Fact]
    public async Task Under_150_sessions_the_long_averages_are_null_and_the_template_rows_say_so()
    {
        Setup(Prices(120));

        var a = await _sut.AnalyzeAsync(Symbol);

        a.Sma50.Should().NotBeNull();
        a.Sma150.Should().BeNull();
        a.Sma200.Should().BeNull();
        a.TrendTemplate!.Criteria.Single(c => c.Key == "price_above_sma200").Value.Should().BeNull();
        a.TrendTemplate.Criteria.Single(c => c.Key == "price_above_sma200").Detail.Should().Contain("Không đủ dữ liệu");
    }

    [Fact]
    public async Task The_trend_template_uses_the_same_numbers_as_the_scalars()
    {
        var prices = Prices(400);
        Setup(prices);

        var a = await _sut.AnalyzeAsync(Symbol);

        var price = prices[^1].Close;
        a.TrendTemplate!.Sessions.Should().Be(400);
        a.TrendTemplate.Criteria.Single(c => c.Key == "price_above_sma50").Value.Should().Be(Math.Round((price / a.Sma50!.Value - 1) * 100, 2));
        a.TrendTemplate.Criteria.Single(c => c.Key == "near_52w_high").Value.Should().Be(a.PctBelowWeek52High);
        a.TrendTemplate.Criteria.Single(c => c.Key == "above_52w_low").Value.Should().Be(a.PctAboveWeek52Low);
    }

    [Fact]
    public async Task Under_20_sessions_the_analysis_carries_no_structure_fields_at_all()
    {
        Setup(Prices(10));

        var a = await _sut.AnalyzeAsync(Symbol);

        a.Sma50.Should().BeNull();
        a.Week52High.Should().BeNull();
        a.Donchian20High.Should().BeNull();
        a.TrendTemplate.Should().BeNull();
    }
}
