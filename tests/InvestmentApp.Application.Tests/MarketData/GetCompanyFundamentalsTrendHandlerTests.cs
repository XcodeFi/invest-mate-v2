using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

/// <summary>Phần Trend của kết quả hồ sơ: tính ở server từ các quý và chỉ số 52 tuần, giá hiện tại lấy một lần từ nguồn giá.</summary>
public class GetCompanyFundamentalsTrendHandlerTests
{
    private readonly Mock<IComprehensiveStockDataProvider> _provider = new();
    private readonly Mock<IMarketDataProvider> _prices = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private GetCompanyFundamentalsQueryHandler Sut() => new(_provider.Object, _cache, _prices.Object);

    private static ComprehensiveStockData Data(List<IncomeStatementItem>? income = null, FinanceIndicators? ind = null) => new()
    {
        Symbol = "FPT",
        Company = new CompanyOverview { CompanyName = "FPT" },
        Indicators = ind ?? new FinanceIndicators { PE = 12m, Min52W = 43.9m, Max52W = 68.2m },
        IncomeStatements = income ?? new List<IncomeStatementItem>
        {
            new() { Period = "Q2/2026", Revenue = 110m, NetProfit = 11m },
            new() { Period = "Q2/2025", Revenue = 100m, NetProfit = 10m },
        }
    };

    private void Provider(ComprehensiveStockData data) =>
        _provider.Setup(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>())).ReturnsAsync(data);

    private void Price(decimal? vnd) =>
        _prices.Setup(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vnd == null ? null : new StockPriceData { Symbol = "FPT", Close = vnd.Value });

    [Fact]
    public async Task attaches_the_trend_with_the_same_quarter_comparison()
    {
        Provider(Data());
        Price(44_600m);

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "fpt" }, default);

        dto.Trend.Should().NotBeNull();
        var q = dto.Trend!.Quarters.First();
        q.Period.Should().Be("Q2/2026");
        q.RevenueYoyPct.Should().Be(10.0m);
        q.RevenueDirection.Should().Be("up");
        dto.Trend.Note.Should().Contain("không có số liệu tháng");
        dto.UnavailableSections.Should().NotContain("trend");
    }

    [Fact]
    public async Task the_current_price_comes_from_the_price_provider_in_thousands_of_dong_to_match_the_52_week_figures()
    {
        Provider(Data());
        Price(44_600m);   // VND từ nguồn giá; đáy/đỉnh 52 tuần của nguồn tính bằng nghìn đồng

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        dto.Trend!.Range52W!.Price.Should().Be(44.6m);
        dto.Trend.Range52W.PositionPct.Should().BeApproximately(2.9m, 0.05m);
    }

    [Fact]
    public async Task a_failing_or_empty_price_source_does_not_fail_the_fundamentals()
    {
        Provider(Data());
        _prices.Setup(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("lỗi giá"));

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        dto.Trend!.Quarters.Should().NotBeEmpty();
        dto.Trend.Range52W!.Low.Should().Be(43.9m);
        dto.Trend.Range52W.Price.Should().BeNull();
        dto.Trend.Range52W.PositionPct.Should().BeNull();
    }

    [Fact]
    public async Task a_price_of_zero_or_missing_is_not_used_as_a_price()
    {
        Provider(Data());
        Price(0m);

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        dto.Trend!.Range52W!.Price.Should().BeNull();
    }

    [Fact]
    public async Task the_price_is_only_requested_when_the_range_needs_it()
    {
        Provider(Data(ind: new FinanceIndicators { PE = 12m }));   // không có đáy/đỉnh 52 tuần

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        _prices.Verify(p => p.GetCurrentPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task nothing_computable_leaves_trend_null_and_lists_it_as_unavailable()
    {
        Provider(Data(income: new List<IncomeStatementItem>(), ind: new FinanceIndicators { PE = 12m }));

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        dto.Trend.Should().BeNull();
        dto.UnavailableSections.Should().Contain("trend");
    }

    [Fact]
    public async Task a_failed_price_lookup_is_not_cached_so_the_next_request_refetches_it()
    {
        Provider(Data());
        _prices.SetupSequence(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("lỗi giá"))
            .ReturnsAsync(new StockPriceData { Symbol = "FPT", Close = 44_600m });

        var first = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);
        var second = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        first.Trend!.Range52W!.PositionPct.Should().BeNull("lần đầu lỗi giá: vị trí để trống");
        second.Trend!.Range52W!.PositionPct.Should().NotBeNull("lần sau thử lại và có giá");
        _prices.Verify(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task an_empty_or_zero_price_is_not_cached_either()
    {
        Provider(Data());
        Price(0m);

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);
        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        _prices.Verify(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task a_symbol_with_no_52_week_range_is_cached_normally_because_it_never_needed_a_price()
    {
        Provider(Data(ind: new FinanceIndicators { PE = 12m }));

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);
        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        _provider.Verify(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(43.9, null)]
    [InlineData(null, 68.2)]
    public async Task a_half_range_has_no_place_for_a_price_so_the_price_source_is_not_called_and_the_result_is_cached(double? low, double? high)
    {
        Provider(Data(ind: new FinanceIndicators { PE = 12m, Min52W = (decimal?)low, Max52W = (decimal?)high }));
        Price(null);

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);
        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        _prices.Verify(p => p.GetCurrentPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider.Verify(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task the_trend_is_cached_with_the_rest_so_the_price_is_fetched_once()
    {
        Provider(Data());
        Price(44_600m);

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);
        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "FPT" }, default);

        _provider.Verify(p => p.GetComprehensiveDataAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
        _prices.Verify(p => p.GetCurrentPriceAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
    }
}
