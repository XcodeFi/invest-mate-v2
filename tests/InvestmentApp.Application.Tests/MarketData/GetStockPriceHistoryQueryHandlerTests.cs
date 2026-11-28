using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

/// <summary>
/// Endpoint lịch sử giá (trang symbol-timeline dùng) trước đây ưu tiên kho đã lưu: nến thật nhưng CHƯA
/// điều chỉnh theo sự kiện quyền. Nay ưu tiên nguồn đã điều chỉnh; không có thì mới tới kho, rồi chuỗi dự phòng.
/// </summary>
public class GetStockPriceHistoryQueryHandlerTests
{
    private readonly Mock<IStockPriceRepository> _repo = new();
    private readonly Mock<IMarketDataProvider> _provider = new();
    private readonly GetStockPriceHistoryQueryHandler _sut;
    private static readonly DateTime D = new(2026, 9, 18);

    public GetStockPriceHistoryQueryHandlerTests()
    {
        _sut = new GetStockPriceHistoryQueryHandler(_repo.Object, _provider.Object);
        _repo.Setup(r => r.GetBySymbolAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPrice>());
    }

    private static GetStockPriceHistoryQuery Q() => new() { Symbol = "FPT", From = D.AddDays(-5), To = D.AddDays(5) };

    private void Provider(params StockPriceData[] bars) =>
        _provider.Setup(p => p.GetHistoricalPricesAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bars.ToList());

    private void Stored(params StockPrice[] rows) =>
        _repo.Setup(r => r.GetBySymbolAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.ToList());

    private static StockPriceData Bar(decimal close, bool adjusted, decimal range = 500m) => new()
    {
        Symbol = "FPT", Date = D, Open = close, High = close + range, Low = close - range, Close = close, Volume = 10, IsAdjusted = adjusted
    };

    [Fact]
    public async Task adjusted_provider_wins_over_stored_unadjusted_prices()
    {
        Provider(Bar(63_715m, true));
        Stored(new StockPrice("FPT", D, 71_000m, 72_000m, 70_500m, 71_700m, 5));

        var r = await _sut.Handle(Q(), default);

        r.Should().ContainSingle().Which.Close.Should().Be(63_715m);
        r[0].Adjusted.Should().BeTrue();
    }

    [Fact]
    public async Task falls_back_to_stored_real_candles_flagged_unadjusted_when_provider_is_unadjusted()
    {
        Provider(Bar(45_900m, false, 0m));   // dự phòng graph: O=H=L=C
        Stored(new StockPrice("FPT", D, 71_000m, 72_000m, 70_500m, 71_700m, 5));

        var r = await _sut.Handle(Q(), default);

        r.Should().ContainSingle().Which.Close.Should().Be(71_700m, "kho có nến thật, hơn chuỗi chỉ có giá đóng cửa");
        r[0].Adjusted.Should().BeFalse();
    }

    [Fact]
    public async Task uses_provider_fallback_flagged_unadjusted_when_store_is_empty()
    {
        Provider(Bar(45_900m, false, 0m));

        var r = await _sut.Handle(Q(), default);

        r.Should().ContainSingle().Which.Close.Should().Be(45_900m);
        r[0].Adjusted.Should().BeFalse();
    }

    [Fact]
    public async Task empty_everywhere_returns_empty()
    {
        Provider();

        (await _sut.Handle(Q(), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task adjusted_result_does_not_touch_the_store()
    {
        Provider(Bar(63_715m, true));

        await _sut.Handle(Q(), default);

        _repo.Verify(r => r.GetBySymbolAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
