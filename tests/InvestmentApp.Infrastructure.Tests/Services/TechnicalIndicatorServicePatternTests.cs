using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>Phân tích kỹ thuật kèm ứng viên mô hình Cốc tay cầm: chỉ khi chuỗi đã điều chỉnh, và chỉ là danh sách (0 hoặc 1 ứng viên).</summary>
public class TechnicalIndicatorServicePatternTests
{
    private readonly Mock<IMarketDataProvider> _market = new();
    private TechnicalIndicatorService Sut() => new(_market.Object);

    private static List<StockPriceData> CupBars(bool adjusted)
    {
        var bars = new List<StockPriceData>();
        var price = 60m;
        var start = new DateTime(2025, 1, 1);
        foreach (var (sessions, end) in new[] { (70, 100m), (25, 72m), (40, 98m), (7, 90m), (3, 96m) })
        {
            for (var k = 1; k <= sessions; k++)
            {
                var close = price + (end - price) * k / sessions;
                bars.Add(new StockPriceData
                {
                    Symbol = "TST", Date = start.AddDays(bars.Count), Open = close, High = close * 1.004m, Low = close * 0.996m,
                    Close = close, Volume = 1_000_000, IsAdjusted = adjusted
                });
            }
            price = end;
        }
        return bars;
    }

    private void Prices(List<StockPriceData> bars) =>
        _market.Setup(m => m.GetHistoricalPricesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(bars);

    [Fact]
    public async Task an_adjusted_series_with_a_cup_returns_one_candidate()
    {
        Prices(CupBars(adjusted: true));

        var result = await Sut().AnalyzeAsync("TST");

        result.PatternCandidates.Should().ContainSingle();
        result.PatternCandidates[0].Type.Should().Be("cup-with-handle");
        result.PatternCandidates[0].State.Should().Be("near-pivot");
    }

    [Fact]
    public async Task an_unadjusted_fallback_series_gets_no_candidate_because_splits_would_fake_a_cup()
    {
        Prices(CupBars(adjusted: false));

        var result = await Sut().AnalyzeAsync("TST");

        result.PatternCandidates.Should().BeEmpty();
    }

    [Fact]
    public async Task a_series_with_no_cup_returns_an_empty_list_not_null()
    {
        Prices(Enumerable.Range(0, 80).Select(i => new StockPriceData
        {
            Symbol = "TST", Date = new DateTime(2025, 1, 1).AddDays(i), Open = 50, High = 51, Low = 49, Close = 50 + i * 0.1m, Volume = 1_000_000, IsAdjusted = true
        }).ToList());

        var result = await Sut().AnalyzeAsync("TST");

        result.PatternCandidates.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task too_little_data_still_returns_an_empty_candidate_list()
    {
        Prices(CupBars(adjusted: true).Take(10).ToList());

        var result = await Sut().AnalyzeAsync("TST");

        result.PatternCandidates.Should().BeEmpty();
    }
}
