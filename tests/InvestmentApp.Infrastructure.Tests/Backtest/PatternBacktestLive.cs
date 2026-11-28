using InvestmentApp.Domain.Constants;
using InvestmentApp.Infrastructure.Repositories;
using MongoDB.Driver;
using Xunit;

namespace InvestmentApp.Infrastructure.Tests.Backtest;

/// <summary>Fact chỉ chạy khi PATTERN_BACKTEST=1; không thì hiện là BỎ QUA (không phải "đạt") trong báo cáo test.</summary>
public sealed class LiveBacktestFactAttribute : FactAttribute
{
    public LiveBacktestFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PATTERN_BACKTEST") != "1") Skip = "Đặt PATTERN_BACKTEST=1 (kèm PATTERN_BACKTEST_MONGO_CONN và PATTERN_BACKTEST_MONGO_DB) để chạy back-test trên dữ liệu thật.";
    }
}

/// <summary>
/// Chạy back-test trên chuỗi giá đã điều chỉnh đã lưu của VN30. CHỈ ĐỌC: dùng đúng <see cref="AdjustedPriceSeriesStore.GetAsync"/> (một lệnh Find theo _id), không ghi vào
/// cơ sở dữ liệu, không gọi nguồn bên ngoài; việc ghi duy nhất là tệp báo cáo. KHÔNG có đường dự phòng tới cấu hình dev (nó trỏ vào cơ sở dữ liệu prod): bắt buộc đặt
/// PATTERN_BACKTEST_MONGO_CONN và PATTERN_BACKTEST_MONGO_DB tường minh. Chuỗi kết nối không bao giờ được in hay lưu; mọi lỗi kết nối chỉ nêu TÊN KIỂU ngoại lệ
/// (thông báo của driver có thể chứa tên máy chủ hoặc thông tin đăng nhập). Tên cơ sở dữ liệu (không phải máy chủ) được in ở đầu báo cáo.
/// Cách chạy: <c>PATTERN_BACKTEST=1 PATTERN_BACKTEST_MONGO_CONN=... PATTERN_BACKTEST_MONGO_DB=... dotnet test tests/InvestmentApp.Infrastructure.Tests --filter PatternBacktestLive</c>;
/// báo cáo ghi ra PATTERN_BACKTEST_OUT (mặc định là thư mục tạm).
/// </summary>
public class PatternBacktestLive
{
    [LiveBacktestFact]
    public async Task run_on_vn30_adjusted_series_read_only()
    {
        var (conn, dbName) = ResolveConnection(Environment.GetEnvironmentVariable);
        var store = new AdjustedPriceSeriesStore(OpenReadOnly(conn, dbName));
        Console.WriteLine($"Back-test CHỈ ĐỌC, cơ sở dữ liệu: {dbName}");

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
        var report = $"Cơ sở dữ liệu: {dbName} (chỉ đọc)\n" + PatternBacktestCore.Format(kinds.Select(k => PatternBacktestCore.Summarise(k, results)).ToList(), loaded, from ?? DateTime.MinValue, to ?? DateTime.MinValue);
        if (failed.Count > 0) report += $"\nMã lỗi khi đọc ({failed.Count}): {string.Join(", ", failed)}\n";

        var path = Environment.GetEnvironmentVariable("PATTERN_BACKTEST_OUT") ?? Path.Combine(Path.GetTempPath(), "pattern-backtest-report.txt");
        await File.WriteAllTextAsync(path, report);
        Console.WriteLine(report);
        Console.WriteLine($"Báo cáo đã ghi: {path}");
    }

    /// <summary>Kết nối và cơ sở dữ liệu đều phải khai báo tường minh qua biến môi trường; thiếu cái nào cũng dừng, không có đường dự phòng.</summary>
    internal static (string Conn, string Db) ResolveConnection(Func<string, string?> env)
    {
        var conn = env("PATTERN_BACKTEST_MONGO_CONN");
        var db = env("PATTERN_BACKTEST_MONGO_DB");
        if (string.IsNullOrWhiteSpace(conn)) throw new InvalidOperationException("Đặt PATTERN_BACKTEST_MONGO_CONN (kết nối chỉ đọc); không có đường dự phòng tới cấu hình dev.");
        if (string.IsNullOrWhiteSpace(db)) throw new InvalidOperationException("Đặt PATTERN_BACKTEST_MONGO_DB (tên cơ sở dữ liệu); không có mặc định.");
        return (conn, db);
    }

    /// <summary>Mở cơ sở dữ liệu; mọi lỗi dựng kết nối chỉ nêu tên kiểu ngoại lệ, không giữ ngoại lệ gốc (thông báo của driver có thể chứa chuỗi kết nối).</summary>
    internal static IMongoDatabase OpenReadOnly(string conn, string db)
    {
        try
        {
            return new MongoClient(conn).GetDatabase(db);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Không dựng được kết nối ({ex.GetType().Name}); kiểm tra PATTERN_BACKTEST_MONGO_CONN.");
        }
    }
}
