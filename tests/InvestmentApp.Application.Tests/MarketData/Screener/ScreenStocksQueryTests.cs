using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.ScreenStocks;
using InvestmentApp.Application.MarketData.Screener;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData.Screener;

public class ScreenStocksQueryTests
{
    private static ScreenerRow Row(string symbol, string floor = "HOSE", decimal roe = 18m)
        => new(symbol, floor, "8000", 20_000m, 50_000m, 1_000_000m, 50_000m, 12m, 1.5m, roe, 8m, 500m, 60, 70, 55, 1.1m);

    private static (ScreenStocksQueryHandler Handler, Mock<IMarketScreenerService> Service) Build(params ScreenerRow[] rows)
    {
        var service = new Mock<IMarketScreenerService>();
        service.Setup(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ScreenerSnapshot
        {
            Rows = rows,
            AsOf = DateTimeOffset.UtcNow,
            Hose = new HoseLists(Array.Empty<HoseStatusEntry>(), Array.Empty<HoseDelisting>())
        });
        return (new ScreenStocksQueryHandler(service.Object), service);
    }

    [Fact]
    public async Task defaults_apply_the_adr_thresholds_and_exclude_upcom()
    {
        var (handler, _) = Build(Row("AAA"), Row("UUU", "UPCOM"), Row("LOW", roe: 5m));

        var result = await handler.Handle(new ScreenStocksQuery(), CancellationToken.None);

        result.Passed.Select(p => p.Symbol).Should().Equal("AAA");
    }

    [Fact]
    public async Task thresholds_and_floors_are_applied_in_memory_and_the_source_is_asked_once_per_request()
    {
        var (handler, service) = Build(Row("AAA"), Row("HNX1", "HNX"));

        var onlyHnx = await handler.Handle(new ScreenStocksQuery { Floors = "hnx" }, CancellationToken.None);
        var both = await handler.Handle(new ScreenStocksQuery { Floors = "HOSE,HNX", MinRoe = 20m }, CancellationToken.None);

        onlyHnx.Passed.Select(p => p.Symbol).Should().Equal("HNX1");
        both.Passed.Should().BeEmpty("MinRoe 20 loại cả hai");
        service.Verify(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task unknown_floor_is_an_argument_error_not_a_silent_default()
    {
        var (handler, service) = Build(Row("AAA"));

        var act = async () => await handler.Handle(new ScreenStocksQuery { Floors = "HOSE,NYSE" }, CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("NYSE");
        service.Verify(s => s.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task numeric_params_are_clamped()
    {
        var (handler, _) = Build(Row("AAA"));

        // MinRs3m 500 → 99: mã có rs3m 70 bị loại; ExcludedTake khổng lồ bị kẹp về trần.
        var result = await handler.Handle(new ScreenStocksQuery { MinRs3m = 500, IncludeExcluded = true, ExcludedTake = 1_000_000 }, CancellationToken.None);

        result.Passed.Should().BeEmpty();
        result.Excluded.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("ab cd")]
    [InlineData("TOOLONGSYMBOL1")]
    [InlineData("A;B")]
    public async Task a_malformed_lookup_symbol_is_refused(string symbol)
    {
        var (handler, _) = Build(Row("AAA"));

        var act = async () => await handler.Handle(new ScreenStocksQuery { LookupSymbol = symbol }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task lookup_symbol_is_normalised_and_explained()
    {
        var (handler, _) = Build(Row("LOW", roe: 5m));

        var result = await handler.Handle(new ScreenStocksQuery { LookupSymbol = " low " }, CancellationToken.None);

        result.Lookup!.Symbol.Should().Be("LOW");
        result.Lookup.State.Should().Be("excluded");
    }

    [Fact]
    public async Task the_default_order_is_alphabetical_with_no_score()
    {
        var (handler, _) = Build(Row("ZZZ"), Row("AAA"));

        var result = await handler.Handle(new ScreenStocksQuery(), CancellationToken.None);

        result.Passed.Select(p => p.Symbol).Should().Equal("AAA", "ZZZ");
    }
}
