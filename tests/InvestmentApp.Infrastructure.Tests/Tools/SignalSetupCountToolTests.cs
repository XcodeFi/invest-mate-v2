using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services.Signals;
using InvestmentApp.Infrastructure.Tests.Services.Signals;
using Xunit.Abstractions;

namespace InvestmentApp.Infrastructure.Tests.Tools;

/// <summary>
/// Công cụ CHỈ ĐỌC đếm cỡ mẫu Pullback trên các chuỗi giá đã điều chỉnh đã lưu. Chạy trên DB thật chỉ khi đặt biến môi trường
/// (xem <see cref="Live_count_reads_persisted_series_when_configured"/>).
/// </summary>
public class SignalSetupCountToolTests
{
    private readonly ITestOutputHelper _output;
    public SignalSetupCountToolTests(ITestOutputHelper output) => _output = output;

    private static List<StockPriceData> Prices(string symbol, int n, int seed)
        => PullbackBacktestTests.Synthetic(n, seed).Select(b => new StockPriceData
        {
            Symbol = symbol, Date = b.Date, Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = 1000, IsAdjusted = true
        }).ToList();

    [Fact]
    public void count_ignores_a_bar_of_today_that_is_still_forming_before_1500_VN()
    {
        var prices = Prices("AAA", 700, 11);
        var lastDate = prices[^1].Date;
        var midday = new DateTimeOffset(lastDate.Year, lastDate.Month, lastDate.Day, 5, 0, 0, TimeSpan.Zero);   // 12:00 giờ VN

        SignalSetupCountTool.Count("AAA", prices, MarketRules.For(null), midday).Bars.Should().Be(699);
        SignalSetupCountTool.Count("AAA", prices, MarketRules.For(null), midday.AddHours(4)).Bars.Should().Be(700);   // 16:00 giờ VN
    }

    [Fact]
    public void count_matches_the_backtest_pipeline_for_one_symbol()
    {
        var prices = Prices("AAA", 700, 11);
        var expected = PullbackBacktest.Run(PullbackBacktestTests.Synthetic(700, 11), MarketRules.For(null));

        var row = SignalSetupCountTool.Count("AAA", prices, MarketRules.For(null));

        row.Bars.Should().Be(700);
        row.Signals.Should().Be(expected.Signals.Count);
        row.Completed.Should().Be(expected.Summary.Completed);
        row.Skipped.Should().Be(expected.Summary.Skipped);
        row.Open.Should().Be(expected.Summary.Open);
        row.Signals.Should().BeGreaterThan(0);
    }

    [Fact]
    public void count_sorts_unsorted_input_by_date()
    {
        var sorted = Prices("AAA", 600, 23);
        var shuffled = sorted.AsEnumerable().Reverse().ToList();

        var a = SignalSetupCountTool.Count("AAA", sorted, MarketRules.For(null));
        var b = SignalSetupCountTool.Count("AAA", shuffled, MarketRules.For(null));

        b.Should().Be(a);
    }

    [Fact]
    public void a_short_series_is_flagged_and_gives_no_signals()
    {
        var row = SignalSetupCountTool.Count("NEW", Prices("NEW", 90, 5), MarketRules.For(null));

        row.Short.Should().BeTrue();
        row.Signals.Should().Be(0, "dưới 100 phiên khởi động thì không có tín hiệu");
    }

    [Fact]
    public void table_has_the_five_columns_per_symbol()
    {
        var rows = new[]
        {
            new SignalSetupCountTool.Row("FPT", 740, new DateTime(2026, 11, 27), 12, 9, 2, 1),
            new SignalSetupCountTool.Row("NEW", 120, new DateTime(2026, 11, 27), 0, 0, 0, 0),
        };

        var text = SignalSetupCountTool.FormatTable(rows);

        text.Should().Contain("symbol").And.Contain("bars").And.Contain("signals").And.Contain("completed").And.Contain("skipped").And.Contain("open");
        text.Should().Contain("FPT").And.Contain("740").And.Contain("2026-11-27");
        text.Should().Contain("short(<250)");
    }

    [Fact]
    public void distribution_buckets_count_only_usable_symbols_and_prints_no_symbol()
    {
        var rows = new[]
        {
            new SignalSetupCountTool.Row("AAA", 740, null, 3, 0, 0, 0),    // 0
            new SignalSetupCountTool.Row("BBB", 740, null, 6, 3, 0, 0),    // 1-4
            new SignalSetupCountTool.Row("CCC", 740, null, 10, 7, 1, 0),   // 5-9
            new SignalSetupCountTool.Row("DDD", 740, null, 15, 12, 1, 1),  // 10-19
            new SignalSetupCountTool.Row("EEE", 740, null, 25, 22, 2, 0),  // 20-29
            new SignalSetupCountTool.Row("FFF", 740, null, 40, 31, 3, 0),  // >=30
            new SignalSetupCountTool.Row("GGG", 100, null, 0, 0, 0, 0),    // short: không tính vào phân bố
        };

        var text = SignalSetupCountTool.FormatDistribution(rows);

        text.Should().Contain("symbols: 7").And.Contain("with >= 250 bars: 6").And.Contain("short: 1");
        text.Should().Contain("0: 1; 1-4: 1; 5-9: 1; 10-19: 1; 20-29: 1; >=30: 1");
        text.Should().Contain("min 0").And.Contain("max 31").And.Contain("median 9.5");
        foreach (var sym in new[] { "AAA", "BBB", "FFF", "GGG" }) text.Should().NotContain(sym);
    }

    [Fact]
    public void distribution_of_nothing_is_not_a_crash()
    {
        var text = SignalSetupCountTool.FormatDistribution(Array.Empty<SignalSetupCountTool.Row>());

        text.Should().Contain("symbols: 0").And.Contain("median n/a");
    }

    [Fact]
    public void tool_source_contains_no_mongo_write_api_and_builds_no_repository()
    {
        // Rào chắn tĩnh: công cụ chỉ đọc. Nếu ai thêm đường ghi, test này đỏ.
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Tools", "SignalSetupCountTool.cs");
        var source = File.ReadAllText(path);

        foreach (var forbidden in new[] { "InsertOne", "InsertMany", "UpdateOne", "UpdateMany", "ReplaceOne", "DeleteOne", "DeleteMany",
                                          "BulkWrite", "FindOneAndUpdate", "FindOneAndReplace", "FindOneAndDelete", "Indexes", "DropCollection",
                                          "RunCommand", "SaveAsync", "new AdjustedPriceSeriesStore(", "IAdjustedPriceSeriesStore", "Repository" })
            source.Should().NotContain(forbidden, $"công cụ đếm không được có đường ghi ({forbidden})");
    }

    [Fact]
    public async Task Live_count_reads_persisted_series_when_configured()
    {
        var conn = Environment.GetEnvironmentVariable("SIGNALS_MONGO_CONN");
        var db = Environment.GetEnvironmentVariable("SIGNALS_MONGO_DB");
        var mode = Environment.GetEnvironmentVariable("SIGNALS_MODE") ?? "table";   // table | summary
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(db))
        {
            _output.WriteLine("Skipped — set SIGNALS_MONGO_CONN, SIGNALS_MONGO_DB (+ SIGNALS_MODE=table|summary, SIGNALS_EXCHANGE=HOSE|HNX|UPCOM, SIGNALS_SYMBOLS=FPT,VNM) to run read-only.");
            return;
        }

        var symbols = Environment.GetEnvironmentVariable("SIGNALS_SYMBOLS")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var output = await SignalSetupCountTool.RunLiveAsync(conn, db, mode, Environment.GetEnvironmentVariable("SIGNALS_EXCHANGE"), symbols);
        _output.WriteLine(output);
    }
}
