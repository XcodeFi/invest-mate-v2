using System.Globalization;
using System.Text;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Patterns;

namespace InvestmentApp.Infrastructure.Tests.Backtest;

public enum EventKind { Cup, Donchian20, Donchian55, BearTrap }

/// <summary>Một tín hiệu: quyết định chỉ dùng dữ liệu tới hết phiên <see cref="SignalIndex"/>; vào lệnh ở giá mở cửa phiên kế tiếp.</summary>
public sealed record BacktestSignal(string Symbol, EventKind Kind, int SignalIndex, DateTime SignalDate, decimal Level, decimal Stop);

public enum TradeOutcome { Success, Trap, Timeout, Skipped, NotObserved }

public sealed record TradeResult(BacktestSignal Signal, TradeOutcome Outcome, decimal Entry, decimal R, decimal Adverse2Pct, decimal AdverseFullPct, int Sessions, bool FailedWithin5);

public sealed record KindSummary(EventKind Kind, int Episodes, int Success, int Trap, int Timeout, int Skipped, int NotObserved, int FailedWithin5,
    double[] Adverse2, double[] AdverseFull, List<(string Month, int Count)> ByMonth, int DateClusters, int MaxSameDate);

/// <summary>
/// Lõi back-test (CHỈ dùng trong dự án test, không nằm trên đường chạy sản phẩm): sinh tín hiệu từ các bộ dò đã có, mô phỏng từng lệnh theo luật đã thống nhất, rồi tổng hợp
/// theo loại sự kiện. Không có lệnh gọi mạng hay ghi. Không tính phần trăm thành công: chỉ đếm, và báo phân bố biên độ đi ngược.
/// Luật: một sự kiện mỗi mã mỗi 20 phiên; vào ở giá mở cửa phiên sau tín hiệu, bỏ qua nếu mở cửa kịch trần (+7% so với đóng cửa tín hiệu);
/// R = mức − cắt lỗ; thành công = chạm mức + 2R trước cắt lỗ; một nến chạm cả hai thì tính là cắt lỗ; hết 20 phiên là hết hạn.
/// </summary>
public static class PatternBacktestCore
{
    public const int CooldownSessions = 20;
    public const int TimeoutSessions = 20;
    public const decimal LimitUpFactor = 1.07m;
    public const int StopLookback = 10;
    public const decimal StopFactor = 0.99m;
    public const int FailWindow = 5;

    public static List<BacktestSignal> Signals(string symbol, IReadOnlyList<StockPriceData> input)
    {
        var bars = input.OrderBy(b => b.Date).ToList();
        var all = new List<BacktestSignal>();
        all.AddRange(Donchian(symbol, bars, 20, EventKind.Donchian20));
        all.AddRange(Donchian(symbol, bars, 55, EventKind.Donchian55));
        all.AddRange(Cups(symbol, bars));
        all.AddRange(BearTraps(symbol, bars));
        return all.OrderBy(s => s.SignalIndex).ThenBy(s => s.Kind).ToList();
    }

    /// <summary>Phiên đóng cửa trên đỉnh N phiên trước (nến phá vỡ ĐẦU TIÊN); cắt lỗ = đáy 10 phiên trước × 0,99.</summary>
    public static List<BacktestSignal> Donchian(string symbol, List<StockPriceData> bars, int n, EventKind kind)
    {
        var list = new List<BacktestSignal>();
        var last = int.MinValue / 2;
        for (var t = n + 1; t < bars.Count; t++)
        {
            var pivot = MaxHigh(bars, t - n, t - 1);
            var prev = MaxHigh(bars, t - 1 - n, t - 2);
            if (!(bars[t].Close > pivot) || bars[t - 1].Close > prev || t - last < CooldownSessions) continue;
            var stop = MinLow(bars, Math.Max(0, t - StopLookback), t - 1) * StopFactor;
            if (stop >= pivot) continue;
            list.Add(new BacktestSignal(symbol, kind, t, bars[t].Date, pivot, stop));
            last = t;
        }
        return list;
    }

    /// <summary>Ngày ứng viên Cốc tay cầm lần đầu chuyển sang trạng thái đã vượt pivot; mức = pivot, cắt lỗ = cắt lỗ của chính ứng viên.</summary>
    public static List<BacktestSignal> Cups(string symbol, List<StockPriceData> bars)
    {
        var list = new List<BacktestSignal>();
        var last = int.MinValue / 2;
        for (var t = 60; t < bars.Count; t++)
        {
            if (t - last < CooldownSessions || bars[t].Close <= bars[t - 1].Close) continue;
            var c = CupWithHandleDetector.Detect(bars.Take(t + 1).ToList());
            if (c == null || !c.State.StartsWith("broke-out", StringComparison.Ordinal)) continue;
            list.Add(new BacktestSignal(symbol, EventKind.Cup, t, bars[t].Date, c.PivotPrice, c.StopPrice));
            last = t;
        }
        return list;
    }

    /// <summary>Bẫy giảm giá: tín hiệu là phiên đóng cửa quay lại trên hỗ trợ (biết ngay ở phiên đó, không cần đợi t+5); cắt lỗ = đáy đợt thủng giả × 0,99.</summary>
    public static List<BacktestSignal> BearTraps(string symbol, List<StockPriceData> bars)
    {
        var byDate = bars.Select((b, i) => (b.Date, i)).ToDictionary(x => x.Date, x => x.i);
        return TrapDetector.Detect(bars, new TrapOptions { MaxEvents = int.MaxValue })
            .Where(e => e.Type == "bear-trap" && byDate.ContainsKey(e.ReturnDate))
            .Select(e => new BacktestSignal(symbol, EventKind.BearTrap, byDate[e.ReturnDate], e.ReturnDate, e.LevelPrice, e.StopPrice))
            .ToList();
    }

    public static TradeResult Simulate(BacktestSignal s, IReadOnlyList<StockPriceData> input)
    {
        var bars = input.OrderBy(b => b.Date).ToList();
        var e = s.SignalIndex + 1;
        var r = s.Level - s.Stop;
        var failed5 = FailedWithin(bars, s);
        if (e >= bars.Count || r <= 0) return new TradeResult(s, TradeOutcome.Skipped, 0, r, 0, 0, 0, failed5);
        var entry = bars[e].Open;
        // Không khớp được: mở cửa khoá kịch trần (đã làm tròn XUỐNG bước giá HOSE), hoặc mở cửa đã ở/trên mục tiêu (R đo từ MỨC, nên gap vượt mục tiêu
        // mà chưa tới kịch trần sẽ thành "thành công" không có lãi thật).
        var target = s.Level + 2m * r;
        if (entry <= 0 || entry >= LimitUpPrice(bars[s.SignalIndex].Close) || entry >= target)
            return new TradeResult(s, TradeOutcome.Skipped, entry, r, 0, 0, 0, failed5);

        var outcome = TradeOutcome.Timeout;
        var exit = Math.Min(bars.Count - 1, e + TimeoutSessions - 1);
        for (var k = e; k <= Math.Min(bars.Count - 1, e + TimeoutSessions - 1); k++)
        {
            var b = bars[k];
            // Chạm cắt lỗ (kể cả mở cửa đã dưới cắt lỗ) là cắt lỗ; nến chạm cả cắt lỗ lẫn mục tiêu cũng tính là cắt lỗ: kiểm cắt lỗ TRƯỚC.
            if (b.Low <= s.Stop) { outcome = TradeOutcome.Trap; exit = k; break; }
            if (b.High >= target) { outcome = TradeOutcome.Success; exit = k; break; }
        }
        // Chưa đủ 20 phiên quan sát mà chưa có kết cục: KHÔNG phải hết hạn (bị cắt cụt bên phải), tách riêng để không thổi phồng số hết hạn của các tín hiệu mới.
        if (outcome == TradeOutcome.Timeout && e + TimeoutSessions - 1 > bars.Count - 1) outcome = TradeOutcome.NotObserved;
        var low2 = MinLow(bars, e, Math.Min(bars.Count - 1, e + 1));
        var lowAll = MinLow(bars, e, exit);
        return new TradeResult(s, outcome, entry, r, Drop(entry, low2), Drop(entry, lowAll), exit - e + 1, failed5);
    }

    /// <summary>Có phiên nào trong t+1..t+5 đóng cửa dưới mức không (với đường phá vỡ Donchian: phá vỡ "thất bại" theo định nghĩa của bộ dò bẫy).</summary>
    private static bool FailedWithin(List<StockPriceData> bars, BacktestSignal s)
    {
        if (s.Kind != EventKind.Donchian20 && s.Kind != EventKind.Donchian55 && s.Kind != EventKind.Cup) return false;
        for (var k = s.SignalIndex + 1; k <= Math.Min(bars.Count - 1, s.SignalIndex + FailWindow); k++)
            if (bars[k].Close < s.Level) return true;
        return false;
    }

    /// <summary>
    /// Giá kịch trần HOSE: đóng cửa × 1,07 làm tròn XUỐNG bước giá (dưới 10.000đ: 10đ; 10.000 đến 49.950đ: 50đ; từ 50.000đ: 100đ). Chuỗi giá có thể tính bằng nghìn đồng
    /// (cổ phiếu VN30 dưới 1.000 nghìn) hoặc bằng đồng (từ 1.000): đoán đơn vị theo độ lớn.
    /// </summary>
    public static decimal LimitUpPrice(decimal close)
    {
        var scale = close >= 1000m ? 1m : 1000m;
        var raw = close * scale * LimitUpFactor;
        var tick = raw < 10_000m ? 10m : raw < 50_000m ? 50m : 100m;
        return Math.Floor(raw / tick) * tick / scale;
    }

    private static decimal Drop(decimal entry, decimal low) => entry <= 0 ? 0 : Math.Max(0m, (entry - low) / entry);

    public static KindSummary Summarise(EventKind kind, IReadOnlyList<TradeResult> all)
    {
        var mine = all.Where(r => r.Signal.Kind == kind).ToList();
        var done = mine.Where(r => r.Outcome is TradeOutcome.Success or TradeOutcome.Trap or TradeOutcome.Timeout).ToList();
        var months = done.GroupBy(r => r.Signal.SignalDate.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        var dates = done.Select(r => r.Signal.SignalDate.Date).OrderBy(d => d).ToList();
        var clusters = 0;
        DateTime? prev = null;
        foreach (var d in dates)
        {
            if (prev == null || (d - prev.Value).TotalDays > 5) clusters++;
            prev = d;
        }
        return new KindSummary(kind, done.Count,
            done.Count(r => r.Outcome == TradeOutcome.Success), done.Count(r => r.Outcome == TradeOutcome.Trap), done.Count(r => r.Outcome == TradeOutcome.Timeout),
            mine.Count(r => r.Outcome == TradeOutcome.Skipped), mine.Count(r => r.Outcome == TradeOutcome.NotObserved), done.Count(r => r.FailedWithin5),
            done.Select(r => (double)r.Adverse2Pct).OrderBy(x => x).ToArray(), done.Select(r => (double)r.AdverseFullPct).OrderBy(x => x).ToArray(),
            months, clusters, dates.GroupBy(d => d).Select(g => g.Count()).DefaultIfEmpty(0).Max());
    }

    /// <summary>Phân vị theo hạng gần nhất trên mảng đã sắp tăng dần; mảng rỗng thì null.</summary>
    public static double? Percentile(double[] sorted, int p)
    {
        if (sorted.Length == 0) return null;
        var idx = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
    }

    public static string Format(IReadOnlyList<KindSummary> rows, int symbols, DateTime from, DateTime to)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Back-test VN30 hiện tại: {symbols} mã, {from:yyyy-MM-dd} đến {to:yyyy-MM-dd}. Chỉ ĐẾM, không phần trăm thành công.");
        sb.AppendLine("Luật: 1 sự kiện/mã/20 phiên; vào mở cửa phiên sau tín hiệu, bỏ nếu khoá kịch trần (làm tròn xuống bước giá) hoặc đã ở/trên mục tiêu; R = mức − cắt lỗ; thành công = chạm mức + 2R trước cắt lỗ;");
        sb.AppendLine("      nến chạm cả hai tính là cắt lỗ; hết hạn sau 20 phiên; tín hiệu chưa đủ 20 phiên quan sát và chưa có kết cục ghi riêng (chưa đủ phiên), không tính là hết hạn. Biên độ đi ngược = (giá vào − đáy thấp nhất) / giá vào.");
        sb.AppendLine();
        sb.AppendLine("loại             | n  | thành công | cắt lỗ (bẫy) | hết hạn | bỏ qua | chưa đủ phiên | thất bại ≤5 phiên | cụm ngày | tối đa/ngày");
        foreach (var r in rows)
            sb.AppendLine($"{r.Kind,-16} | {r.Episodes,2} | {r.Success,10} | {r.Trap,12} | {r.Timeout,7} | {r.Skipped,6} | {r.NotObserved,13} | {r.FailedWithin5,17} | {r.DateClusters,8} | {r.MaxSameDate,11}");
        sb.AppendLine();
        sb.AppendLine("biên độ đi ngược (đơn vị: phần trăm giá vào): 2 phiên đầu | cả lệnh");
        sb.AppendLine("loại             | 2p: p10 / p50 / p90          | cả lệnh: p10 / p50 / p90");
        foreach (var r in rows)
            sb.AppendLine($"{r.Kind,-16} | {Pcts(r.Adverse2),-28} | {Pcts(r.AdverseFull)}");
        sb.AppendLine();
        sb.AppendLine("cụm theo tháng (5 tháng đông nhất):");
        foreach (var r in rows)
            sb.AppendLine($"{r.Kind,-16} | {string.Join(", ", r.ByMonth.Take(5).Select(m => $"{m.Month}: {m.Count}"))}");
        return sb.ToString();
    }

    private static string Pcts(double[] sorted)
    {
        string F(int p) => Percentile(sorted, p) is { } v ? (v * 100).ToString("0.0", CultureInfo.InvariantCulture) : "—";
        return $"{F(10)} / {F(50)} / {F(90)}";
    }

    private static decimal MaxHigh(List<StockPriceData> bars, int from, int to)
    {
        var max = decimal.MinValue;
        for (var k = Math.Max(0, from); k <= to; k++) max = Math.Max(max, bars[k].High);
        return max;
    }

    private static decimal MinLow(List<StockPriceData> bars, int from, int to)
    {
        var min = decimal.MaxValue;
        for (var k = Math.Max(0, from); k <= to; k++) min = Math.Min(min, bars[k].Low);
        return min;
    }
}
