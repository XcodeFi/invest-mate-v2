using System.Globalization;
using System.Text;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Services.Signals;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Tests.Tools;

/// <summary>
/// S1 bước 1 — công cụ CHỈ ĐỌC đếm cỡ mẫu của setup Pullback trên các chuỗi giá đã điều chỉnh đã lưu bền (<c>adjusted_price_series</c>).
/// Với mỗi mã in: số nến, tín hiệu, lệnh đã đóng, lệnh bị bỏ qua, lệnh đang mở. Cuối cùng in phân bố số lệnh đã đóng để quyết định cách
/// nói với người dùng khi phần lớn mã chưa đủ 30 mẫu.
/// Chỉ <c>Find</c>: không dựng repository (hàm dựng tạo chỉ mục = ghi), chỉ dùng phương thức tĩnh chuyển bản ghi → chuỗi nến.
/// Cấu hình chỉ qua biến môi trường (xem test cuối). Sàn mặc định chưa biết → biên 7% cho mọi mã (giả định chặt nhất).
/// </summary>
internal static class SignalSetupCountTool
{
    /// <summary>Số nến tối thiểu để thống kê (cùng ngưỡng với truy vấn bước 2).</summary>
    internal const int MinBars = 250;

    internal sealed record Row(string Symbol, int Bars, DateTime? LastDate, int Signals, int Completed, int Skipped, int Open)
    {
        public bool Short => Bars < MinBars;
    }

    public static Row Count(string symbol, IReadOnlyList<StockPriceData> prices, MarketRules rules, DateTimeOffset? now = null)
    {
        // Cùng quy tắc với dịch vụ: nến của hôm nay còn đang hình thành (trước 15:00 giờ VN) không tính
        var sorted = FormingBar.DropIfForming(prices.OrderBy(p => p.Date).ToList(), p => p.Date, now ?? DateTimeOffset.UtcNow);
        var bars = sorted.Select(p => new Bar(p.Date, p.Open, p.High, p.Low, p.Close)).ToList();
        var result = PullbackBacktest.Run(bars, rules);
        var s = result.Summary;
        return new Row(symbol, bars.Count, bars.Count > 0 ? bars[^1].Date : null, result.Signals.Count, s.Completed, s.Skipped, s.Open);
    }

    public static string FormatTable(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("symbol | bars | last bar   | signals | completed | skipped | open | note");
        foreach (var r in rows.OrderByDescending(r => r.Completed).ThenBy(r => r.Symbol, StringComparer.Ordinal))
            sb.AppendLine($"{r.Symbol,-6} | {r.Bars,4} | {(r.LastDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-"),-10} | {r.Signals,7} | {r.Completed,9} | {r.Skipped,7} | {r.Open,4} | {(r.Short ? "short(<250)" : "")}");
        return sb.ToString();
    }

    /// <summary>Phân bố số lệnh đã đóng trên các mã đủ dài (≥ 250 nến). Không in mã.</summary>
    public static string FormatDistribution(IEnumerable<Row> rows)
    {
        var all = rows.ToList();
        var usable = all.Where(r => !r.Short).ToList();
        var completed = usable.Select(r => r.Completed).OrderBy(x => x).ToList();

        int Bucket(int lo, int hi) => completed.Count(c => c >= lo && c <= hi);
        var median = completed.Count == 0 ? (double?)null
            : completed.Count % 2 == 1 ? completed[completed.Count / 2]
            : (completed[completed.Count / 2 - 1] + completed[completed.Count / 2]) / 2.0;

        var sb = new StringBuilder();
        sb.AppendLine($"symbols: {all.Count}; with >= {MinBars} bars: {usable.Count}; short: {all.Count - usable.Count}");
        sb.AppendLine($"completed trades per usable symbol: 0: {Bucket(0, 0)}; 1-4: {Bucket(1, 4)}; 5-9: {Bucket(5, 9)}; 10-19: {Bucket(10, 19)}; 20-29: {Bucket(20, 29)}; >=30: {Bucket(30, int.MaxValue)}");
        sb.AppendLine($"completed: min {(completed.Count > 0 ? completed[0].ToString(CultureInfo.InvariantCulture) : "n/a")}, median {(median?.ToString("0.0", CultureInfo.InvariantCulture) ?? "n/a")}, max {(completed.Count > 0 ? completed[^1].ToString(CultureInfo.InvariantCulture) : "n/a")}");
        sb.AppendLine($"signals total: {usable.Sum(r => r.Signals)}; skipped total: {usable.Sum(r => r.Skipped)}; open total: {usable.Sum(r => r.Open)}");
        return sb.ToString();
    }

    private static void RegisterConventions()
        => ConventionRegistry.Register("SignalCountConventions", new ConventionPack { new IgnoreExtraElementsConvention(true) }, _ => true);

    /// <summary>mode: table (bảng từng mã + phân bố) | summary (chỉ phân bố, không in mã). Chỉ đọc.</summary>
    public static async Task<string> RunLiveAsync(string conn, string dbName, string mode, string? exchange, IReadOnlyCollection<string>? onlySymbols)
    {
        RegisterConventions();
        var db = new MongoClient(conn).GetDatabase(dbName);
        var col = db.GetCollection<AdjustedPriceSeriesDocument>(AdjustedPriceSeriesStore.CollectionName);
        var docs = await col.Find(FilterDefinition<AdjustedPriceSeriesDocument>.Empty).ToListAsync();

        var rules = MarketRules.For(exchange);
        var rows = new List<Row>();
        foreach (var doc in docs)
        {
            if (onlySymbols is { Count: > 0 } && !onlySymbols.Contains(doc.Symbol, StringComparer.OrdinalIgnoreCase)) continue;
            var snapshot = AdjustedPriceSeriesStore.ToSnapshot(doc);
            rows.Add(Count(doc.Symbol, snapshot.Bars, rules));
        }

        return mode switch
        {
            "table" => FormatTable(rows) + FormatDistribution(rows),
            "summary" => FormatDistribution(rows),
            _ => throw new ArgumentException($"Chế độ lạ: {mode}")
        };
    }
}
