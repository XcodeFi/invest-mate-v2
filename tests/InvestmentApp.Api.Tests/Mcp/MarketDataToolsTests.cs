using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using MediatR;
using ModelContextProtocol;
using Moq;
using Xunit;

namespace InvestmentApp.Api.Tests.Mcp;

public class MarketDataToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    private static TechnicalSeriesResult SeriesOf(int n)
    {
        var r = Build(n);
        // Mọi chuỗi chỉ báo còn lại (ADX, ATR, OBV...) cũng phải có đủ n phần tử, như dữ liệu thật.
        foreach (var prop in typeof(TechnicalSeriesResult).GetProperties()
                     .Where(p => p.PropertyType == typeof(List<decimal?>)))
            if (((List<decimal?>)prop.GetValue(r)!).Count == 0)
                prop.SetValue(r, Enumerable.Range(0, n).Select(i => (decimal?)i).ToList());
        return r;
    }

    private static TechnicalSeriesResult Build(int n) => new()
    {
        Symbol = "FPT",
        Dates = Enumerable.Range(0, n).Select(i => new DateTime(2026, 1, 1).AddDays(i)).ToList(),
        Open = Enumerable.Range(0, n).Select(i => (decimal)i).ToList(),
        High = Enumerable.Range(0, n).Select(i => (decimal)i).ToList(),
        Low = Enumerable.Range(0, n).Select(i => (decimal)i).ToList(),
        Close = Enumerable.Range(0, n).Select(i => (decimal)i).ToList(),
        Volume = Enumerable.Range(0, n).Select(i => (long)i).ToList(),
        Ema20 = Enumerable.Range(0, n).Select(i => i < 20 ? (decimal?)null : i).ToList(),
        Ema200 = Enumerable.Range(0, n).Select(_ => (decimal?)null).ToList(),
        Rsi14 = Enumerable.Range(0, n).Select(i => (decimal?)i).ToList(),
    };

    private void ReturnSeries(int n, out Func<GetTechnicalSeriesQuery?> sent)
        => McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out sent, SeriesOf(n));

    // ---------- get_stock_price / search / overview ----------

    [Fact]
    public async Task get_stock_price_chuan_hoa_ma()
    {
        McpTestContext.Capture<StockPriceDto, GetStockPriceQuery>(_mediator, out var sent, new StockPriceDto());

        await MarketDataTools.GetStockPrice(" fpt ", _mediator.Object, CancellationToken.None);

        sent()!.Symbol.Should().Be("FPT");
    }

    [Fact]
    public async Task get_market_overview_gui_dung_query()
    {
        McpTestContext.Capture<List<MarketOverviewDto>, GetMarketOverviewQuery>(
            _mediator, out var sent, new List<MarketOverviewDto>());

        await MarketDataTools.GetMarketOverview(_mediator.Object, CancellationToken.None);

        sent().Should().NotBeNull();
    }

    [Fact]
    public async Task search_stocks_gui_tu_khoa()
    {
        McpTestContext.Capture<List<StockSearchDto>, SearchStocksQuery>(
            _mediator, out var sent, new List<StockSearchDto>());

        await MarketDataTools.SearchStocks("  fpt  ", _mediator.Object, CancellationToken.None);

        sent()!.Keyword.Should().Be("fpt");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task search_stocks_tu_khoa_rong_bao_ro_can_gui_gi(string bad)
    {
        var act = async () => await MarketDataTools.SearchStocks(bad, _mediator.Object, CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("từ khoá");
    }

    // ---------- adjusted / ema200 (WS1): MCP phải lộ đúng những gì REST lộ ----------

    [Fact]
    public async Task get_technical_series_returns_adjusted_flag_and_the_ema200_array_trimmed_with_the_rest()
    {
        var series = SeriesOf(40);
        series.Adjusted = true;
        series.Ema200 = Enumerable.Range(0, 40).Select(i => i < 30 ? (decimal?)null : 64_000m + i).ToList();
        McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out _, series);

        var result = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None, lastN: 5);

        result["adjusted"]!.GetValue<bool>().Should().BeTrue();
        var ema200 = result["ema200"]!.AsArray();
        ema200.Should().HaveCount(5, "cùng độ dài với các mảng khác sau khi cắt");
        ema200[4]!.GetValue<decimal>().Should().Be(64_039m);
    }

    [Fact]
    public async Task get_technical_series_reports_adjusted_false_for_the_fallback_source()
    {
        var series = SeriesOf(30);
        series.Adjusted = false;
        McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out _, series);

        var result = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);

        result["adjusted"]!.GetValue<bool>().Should().BeFalse("false phải hiện ra, đừng để mất cùng với null");
    }

    [Fact]
    public async Task get_technical_series_omits_adjusted_when_unknown()
    {
        var series = SeriesOf(30);
        series.Adjusted = null;
        McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out _, series);

        var result = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);

        result.AsObject().ContainsKey("adjusted").Should().BeFalse();
    }

    [Fact]
    public async Task get_technical_analysis_reports_adjusted_false_too()
    {
        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out _, new TechnicalAnalysisResult { Symbol = "FPT", Adjusted = false });

        var result = await MarketDataTools.GetTechnicalAnalysis("FPT", _mediator.Object, CancellationToken.None);

        result["adjusted"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task get_price_history_rows_carry_the_adjusted_flag_in_the_mcp_json()
    {
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(_mediator, out _, new List<StockPriceHistoryDto>
        {
            new() { Symbol = "FPT", Date = new DateTime(2026, 9, 18), Close = 63_715m, Adjusted = true },
            new() { Symbol = "FPT", Date = new DateTime(2026, 9, 21), Close = 64_906m, Adjusted = false },
        });

        var rows = await MarketDataTools.GetPriceHistory("FPT", _mediator.Object, CancellationToken.None);
        var json = JsonSerializer.SerializeToNode(rows, McpJsonUtilities.DefaultOptions)!.AsArray();

        json[0]!["adjusted"]!.GetValue<bool>().Should().BeTrue();
        json[1]!["adjusted"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task get_technical_series_and_analysis_expose_adjusted_as_of_and_omit_it_when_null()
    {
        var series = SeriesOf(30);
        series.Adjusted = true;
        series.AdjustedAsOf = new DateTime(2026, 11, 20);
        McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out _, series);
        var s = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);
        s["adjustedAsOf"]!.GetValue<DateTime>().Should().Be(new DateTime(2026, 11, 20));

        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out _, new TechnicalAnalysisResult { Symbol = "FPT", Adjusted = true, AdjustedAsOf = new DateTime(2026, 11, 20) });
        var a = await MarketDataTools.GetTechnicalAnalysis("FPT", _mediator.Object, CancellationToken.None);
        a["adjustedAsOf"]!.GetValue<DateTime>().Should().Be(new DateTime(2026, 11, 20));

        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out _, new TechnicalAnalysisResult { Symbol = "FPT", Adjusted = false, AdjustedAsOf = null });
        (await MarketDataTools.GetTechnicalAnalysis("FPT", _mediator.Object, CancellationToken.None))
            .AsObject().ContainsKey("adjustedAsOf").Should().BeFalse();
    }

    [Fact]
    public async Task get_price_history_rows_carry_adjusted_as_of_in_the_mcp_json()
    {
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(_mediator, out _, new List<StockPriceHistoryDto>
        {
            new() { Symbol = "FPT", Date = new DateTime(2026, 9, 18), Adjusted = true, AdjustedAsOf = new DateTime(2026, 11, 20) },
        });

        var rows = await MarketDataTools.GetPriceHistory("FPT", _mediator.Object, CancellationToken.None);
        var json = JsonSerializer.SerializeToNode(rows, McpJsonUtilities.DefaultOptions)!.AsArray();

        json[0]!["adjustedAsOf"]!.GetValue<DateTime>().Should().Be(new DateTime(2026, 11, 20));
    }

    // ---------- get_price_history ----------

    [Fact]
    public async Task get_price_history_chuan_hoa_ma_va_truyen_ngay()
    {
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(
            _mediator, out var sent, new List<StockPriceHistoryDto>());

        await MarketDataTools.GetPriceHistory(
            " vnm ", _mediator.Object, CancellationToken.None, "2026-01-05", "2026-02-10");

        sent()!.Symbol.Should().Be("VNM");
        sent()!.From.Should().Be(new DateTime(2026, 1, 5));
        sent()!.To.Should().Be(new DateTime(2026, 2, 10));
    }

    [Fact]
    public async Task get_price_history_bo_trong_ngay_thi_lay_3_thang_gan_nhat_nhu_REST()
    {
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(
            _mediator, out var sent, new List<StockPriceHistoryDto>());
        var before = DateTime.UtcNow;

        await MarketDataTools.GetPriceHistory("VNM", _mediator.Object, CancellationToken.None);

        sent()!.To.Should().BeOnOrAfter(before);
        sent()!.From.Should().BeCloseTo(sent()!.To.AddMonths(-3), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("05/01/2026")]
    [InlineData("2026-1-5")]
    [InlineData("hôm qua")]
    public async Task get_price_history_ngay_sai_dinh_dang_bao_ro_can_gui_gi(string bad)
    {
        var act = async () => await MarketDataTools.GetPriceHistory(
            "VNM", _mediator.Object, CancellationToken.None, from: bad);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Contain("YYYY-MM-DD").And.Contain(bad);
        ex.ParamName.Should().Be("from", "lỗi phải chỉ đúng tham số agent gửi sai");
    }

    [Fact]
    public async Task get_price_history_to_sai_dinh_dang_bao_dung_tham_so_to()
    {
        var act = async () => await MarketDataTools.GetPriceHistory(
            "VNM", _mediator.Object, CancellationToken.None, to: "10/02/2026");

        (await act.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("to");
    }

    // ---------- get_technical_analysis ----------

    [Fact]
    public async Task get_technical_analysis_chuan_hoa_ma()
    {
        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out var sent, new TechnicalAnalysisResult { Symbol = "FPT" });

        await MarketDataTools.GetTechnicalAnalysis(" fpt ", _mediator.Object, CancellationToken.None);

        sent()!.Symbol.Should().Be("FPT");
    }

    [Fact]
    public async Task get_technical_analysis_includes_ema200_when_present()
    {
        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out _, new TechnicalAnalysisResult { Symbol = "FPT", Ema200 = 123m, Rsi14 = 55m, Adjusted = true });

        var result = await MarketDataTools.GetTechnicalAnalysis("FPT", _mediator.Object, CancellationToken.None);

        var json = result.ToJsonString();
        json.Should().Contain("\"ema200\":123");
        json.Should().Contain("\"symbol\":\"FPT\"");
        json.Should().Contain("rsi14");
        json.Should().Contain("\"adjusted\":true");
    }

    [Fact]
    public async Task get_technical_analysis_omits_ema200_when_not_enough_sessions()
    {
        // Chưa đủ 200 phiên thì ema200 null và bị bỏ khỏi JSON (tuỳ chọn MCP bỏ trường null), không phải số 0.
        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out _, new TechnicalAnalysisResult { Symbol = "FPT", Ema200 = null, Rsi14 = 55m });

        var result = await MarketDataTools.GetTechnicalAnalysis("FPT", _mediator.Object, CancellationToken.None);

        result.ToJsonString().Should().NotContainEquivalentOf("ema200");
    }

    // ---------- get_technical_series ----------

    [Fact]
    public async Task get_technical_series_chuan_hoa_ma()
    {
        ReturnSeries(60, out var sent);

        await MarketDataTools.GetTechnicalSeries(" fpt ", _mediator.Object, CancellationToken.None);

        sent()!.Symbol.Should().Be("FPT");
    }

    [Fact]
    public async Task get_technical_series_defaults_to_last_30()
    {
        ReturnSeries(60, out _);

        var result = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);

        result["dates"]!.AsArray().Count.Should().Be(30);
        // 30 phiên CUỐI: close 30..59
        result["close"]!.AsArray()[0]!.GetValue<decimal>().Should().Be(30m);
        result["close"]!.AsArray()[29]!.GetValue<decimal>().Should().Be(59m);
    }

    [Theory]
    [InlineData(500, 120)]
    [InlineData(121, 120)]
    [InlineData(1, 5)]
    [InlineData(0, 5)]
    [InlineData(-10, 5)]
    [InlineData(5, 5)]
    [InlineData(120, 120)]
    [InlineData(45, 45)]
    public async Task get_technical_series_clamps_lastN(int asked, int expected)
    {
        ReturnSeries(200, out _);

        var result = await MarketDataTools.GetTechnicalSeries(
            "FPT", _mediator.Object, CancellationToken.None, lastN: asked);

        result["dates"]!.AsArray().Count.Should().Be(expected);
    }

    [Fact]
    public async Task get_technical_series_all_arrays_same_length_after_trim()
    {
        ReturnSeries(61, out _);

        var result = await MarketDataTools.GetTechnicalSeries(
            "FPT", _mediator.Object, CancellationToken.None, lastN: 25);

        var lengths = result.AsObject()
            .Where(kv => kv.Value is JsonArray)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.AsArray().Count);
        lengths.Should().NotBeEmpty();
        lengths.Values.Should().OnlyContain(c => c == 25, "mọi mảng (ngày, OHLCV, 19 chuỗi chỉ báo) phải cùng độ dài");
        lengths.Keys.Should().Contain(new[] { "dates", "open", "close", "volume", "ema20", "rsi14" });
    }

    [Fact]
    public async Task get_technical_series_giu_null_cua_chi_bao_chua_du_phien()
    {
        // 61 phiên, ema20 null ở 19 phiên đầu; lấy 30 cuối thì không còn null, lấy 120 (>61) thì giữ đủ 61 kèm null.
        ReturnSeries(61, out _);

        var last30 = await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);
        ReturnSeries(61, out _);
        var all = await MarketDataTools.GetTechnicalSeries(
            "FPT", _mediator.Object, CancellationToken.None, lastN: 120);

        last30["ema20"]!.AsArray().Count(v => v is null).Should().Be(0);
        all["dates"]!.AsArray().Count.Should().Be(61);
        all["ema20"]!.AsArray().Count(v => v is null).Should().Be(20);
    }

    [Fact]
    public async Task get_technical_series_khong_doi_ket_qua_goc()
    {
        var original = SeriesOf(60);
        McpTestContext.Capture<TechnicalSeriesResult, GetTechnicalSeriesQuery>(_mediator, out _, original);

        await MarketDataTools.GetTechnicalSeries("FPT", _mediator.Object, CancellationToken.None);

        original.Dates.Count.Should().Be(60);
    }
}
