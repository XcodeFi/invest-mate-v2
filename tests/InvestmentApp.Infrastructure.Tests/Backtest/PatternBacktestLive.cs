using System.Text.Json;
using InvestmentApp.Domain.Constants;
using InvestmentApp.Infrastructure.Repositories;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Tests.Backtest;

/// <summary>
/// Chạy back-test trên chuỗi giá đã điều chỉnh đã lưu của VN30. CHỈ chạy khi đặt PATTERN_BACKTEST=1 (mặc định bỏ qua, nên bộ test và CI không chạm cơ sở dữ liệu).
/// CHỈ ĐỌC: dùng đúng <see cref="AdjustedPriceSeriesStore.GetAsync"/> (một lệnh Find theo _id), không ghi, không gọi nguồn bên ngoài.
/// Chuỗi kết nối lấy từ PATTERN_BACKTEST_MONGO_CONN (và PATTERN_BACKTEST_MONGO_DB) hoặc từ appsettings.Development.json; KHÔNG bao giờ được in ra hay lưu,
/// và lỗi chỉ in tên kiểu ngoại lệ (thông báo lỗi của driver có thể chứa tên máy chủ).
/// Cách chạy: <c>PATTERN_BACKTEST=1 dotnet test tests/InvestmentApp.Infrastructure.Tests --filter PatternBacktestLive</c>; báo cáo ghi ra PATTERN_BACKTEST_OUT
/// (mặc định là thư mục tạm).
/// </summary>
public class PatternBacktestLive
{
    [Fact]
    public async Task run_on_vn30_adjusted_series_read_only()
    {
        if (Environment.GetEnvironmentVariable("PATTERN_BACKTEST") != "1") return;

        var (conn, dbName) = ResolveConnection();
        var store = new AdjustedPriceSeriesStore(new MongoClient(conn).GetDatabase(dbName));

        var results = new List<TradeResult>();
        var loaded = 0;
        var failed = new List<string>();
        DateTime? from = null, to = null;
        foreach (var symbol in Vn30Basket.Symbols)
        {
            try
            {
                var snap = await store.GetAsync(symbol);
                if (snap == null || snap.Bars.Count < 120) continue;
                loaded++;
                var bars = snap.Bars.OrderBy(b => b.Date).ToList();
                from = from == null || bars[0].Date < from ? bars[0].Date : from;
                to = to == null || bars[^1].Date > to ? bars[^1].Date : to;
                foreach (var s in PatternBacktestCore.Signals(symbol, bars))
                    results.Add(PatternBacktestCore.Simulate(s, bars));
            }
            catch (Exception ex)
            {
                failed.Add($"{symbol}:{ex.GetType().Name}");   // chỉ tên kiểu: thông báo lỗi có thể lộ máy chủ
            }
        }

        var kinds = Enum.GetValues<EventKind>();
        var report = PatternBacktestCore.Format(kinds.Select(k => PatternBacktestCore.Summarise(k, results)).ToList(), loaded, from ?? DateTime.MinValue, to ?? DateTime.MinValue);
        if (failed.Count > 0) report += $"\nMã lỗi khi đọc ({failed.Count}): {string.Join(", ", failed)}\n";

        var path = Environment.GetEnvironmentVariable("PATTERN_BACKTEST_OUT") ?? Path.Combine(Path.GetTempPath(), "pattern-backtest-report.txt");
        await File.WriteAllTextAsync(path, report);
        Console.WriteLine(report);
        Console.WriteLine($"Báo cáo đã ghi: {path}");
    }

    private static (string Conn, string Db) ResolveConnection()
    {
        var conn = Environment.GetEnvironmentVariable("PATTERN_BACKTEST_MONGO_CONN");
        var db = Environment.GetEnvironmentVariable("PATTERN_BACKTEST_MONGO_DB");
        if (!string.IsNullOrWhiteSpace(conn)) return (conn, string.IsNullOrWhiteSpace(db) ? "InvestmentApp" : db);

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "InvestmentApp.Api", "appsettings.Development.json");
            if (!File.Exists(file)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var c = doc.RootElement.GetProperty("ConnectionStrings").GetProperty("MongoDb").GetString();
            var d = doc.RootElement.GetProperty("MongoDb").GetProperty("DatabaseName").GetString();
            if (!string.IsNullOrWhiteSpace(c) && !string.IsNullOrWhiteSpace(d)) return (c, d);
        }
        throw new InvalidOperationException("Không tìm thấy cấu hình kết nối: đặt PATTERN_BACKTEST_MONGO_CONN.");
    }
}
