namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Một lệnh mô phỏng cho một tín hiệu. Lệnh bị bỏ qua có <see cref="Skipped"/> và lý do; lệnh chưa thoát có ExitDate null.</summary>
public sealed record SimulatedTrade(
    int SignalIndex, DateTime SignalDate, DateTime? EntryDate, decimal? Entry, decimal? Stop, decimal? Target,
    DateTime? ExitDate, decimal? Exit, string? ExitReason, decimal? R, bool Skipped, string? SkipReason)
{
    public bool Completed => ExitDate != null;
    public bool IsOpen => !Skipped && EntryDate != null && ExitDate == null;
}

public sealed record BacktestSummary(int Trades, int Completed, int Skipped, int Open);

/// <summary>
/// Mô phỏng lệnh theo luật thị trường Việt Nam, hàm thuần. Vào ở giá mở cửa phiên d0 (sau tín hiệu); cắt lỗ = vào − 2 × ATR14(t),
/// 1R = vào − cắt lỗ, mục tiêu = vào + 2R. T+2,5: mua ở d0 chưa bán được tới hết d2, chạm cắt lỗ/mục tiêu trong d0..d2 thì thoát ở giá MỞ d3.
/// Từ d3: mở ≤ cắt lỗ → thoát ở giá mở (gap); thấp ≤ cắt lỗ → thoát ở cắt lỗ (cùng phiên chạm cả mục tiêu vẫn tính cắt lỗ); cao ≥ mục tiêu →
/// thoát ở mục tiêu. Thoát rơi vào phiên khoá sàn thì dời tới giá mở phiên đầu tiên sau đó không khoá. Hết 20 phiên (d0..d19) thoát ở đóng cửa d19.
/// Không chồng lệnh. Chi phí: phí 0,15% mỗi chiều, thuế bán 0,1%, trượt giá 0,1% mỗi chiều.
/// </summary>
public static class SetupBacktester
{
    public const decimal FeeRate = 0.0015m;
    public const decimal SellTax = 0.001m;
    public const decimal Slippage = 0.001m;

    /// <summary>Số phiên tối đa giữ lệnh: d0..d19.</summary>
    public const int MaxHoldSessions = 20;

    /// <summary>Phiên d0, d1, d2 chưa bán được (T+2,5); từ d3 mới được bán.</summary>
    private const int FirstSellableDay = 3;

    public static List<SimulatedTrade> Simulate(
        IReadOnlyList<Bar> bars, IReadOnlyList<int> signals, IReadOnlyList<decimal?> atr, MarketRules rules)
    {
        var trades = new List<SimulatedTrade>();
        var busyUntil = -1;   // chỉ số phiên thoát của lệnh gần nhất; int.MaxValue nếu còn mở tới hết dữ liệu

        foreach (var t in signals.OrderBy(x => x))
        {
            if (t < busyUntil) continue;                       // đang giữ lệnh: bỏ tín hiệu
            var d0 = t + 1;
            if (d0 >= bars.Count) continue;                    // chưa có phiên vào lệnh

            var trade = SimulateOne(bars, t, atr, rules, out var exitIndex);
            trades.Add(trade);
            if (!trade.Skipped)
                busyUntil = trade.ExitDate == null ? int.MaxValue : exitIndex;
        }

        return trades;
    }

    public static BacktestSummary Summarize(IEnumerable<SimulatedTrade> trades)
    {
        var list = trades.ToList();
        return new BacktestSummary(list.Count, list.Count(t => t.Completed), list.Count(t => t.Skipped), list.Count(t => t.IsOpen));
    }

    private static SimulatedTrade SimulateOne(
        IReadOnlyList<Bar> bars, int t, IReadOnlyList<decimal?> atrSeries, MarketRules rules, out int exitIndex)
    {
        exitIndex = -1;
        var signalBar = bars[t];
        var d0 = t + 1;

        SimulatedTrade Skip(string reason) => new(t, signalBar.Date, null, null, null, null, null, null, null, null, true, reason);

        if (t > 0 && rules.IsAtLimit(signalBar.Close, bars[t - 1].Close)) return Skip("signal-at-limit");
        if (rules.IsOpenNearCeiling(bars[d0].Open, signalBar.Close)) return Skip("open-near-ceiling");

        var entry = bars[d0].Open;
        var atr = atrSeries[t] ?? 0m;
        var stop = entry - 2m * atr;
        if (stop <= 0m) return Skip("stop-not-positive");
        var oneR = entry - stop;
        if (oneR <= 0m) return Skip("zero-risk");
        var target = entry + 2m * oneR;

        var pendingT25 = false;
        var last = Math.Min(d0 + MaxHoldSessions - 1, bars.Count - 1);
        for (var i = d0; i <= last; i++)
        {
            var bar = bars[i];
            var day = i - d0;
            int? exitAt = null;
            decimal exitPrice = 0m;
            string reason = "";

            if (day < FirstSellableDay)
            {
                // chưa bán được: chỉ ghi nhận đã chạm, thoát ở giá mở d3
                if (bar.Low <= stop || bar.High >= target) pendingT25 = true;
                continue;
            }

            if (pendingT25)
            {
                (exitAt, exitPrice, reason) = (i, bar.Open, "t25");
            }
            else if (bar.Open <= stop)
            {
                (exitAt, exitPrice, reason) = (i, bar.Open, "gap");
            }
            else if (bar.Low <= stop)
            {
                (exitAt, exitPrice, reason) = (i, stop, "stop");
            }
            else if (bar.High >= target)
            {
                (exitAt, exitPrice, reason) = (i, target, "target");
            }
            else if (i == d0 + MaxHoldSessions - 1)
            {
                (exitAt, exitPrice, reason) = (i, bar.Close, "expiry");
            }

            if (exitAt == null) continue;

            // Thoát rơi vào phiên khoá sàn: chờ tới giá mở phiên đầu tiên không khoá
            if (IsLocked(bars, exitAt.Value, rules))
            {
                var j = exitAt.Value + 1;
                while (j < bars.Count && IsLocked(bars, j, rules)) j++;
                if (j >= bars.Count) return OpenTrade(t, signalBar, bars[d0], entry, stop, target);   // chưa bán được tới hết dữ liệu
                (exitAt, exitPrice, reason) = (j, bars[j].Open, "floor-delay");
            }

            exitIndex = exitAt.Value;
            return Close(t, signalBar, bars[d0], entry, stop, target, bars[exitAt.Value], exitPrice, reason, oneR);
        }

        // Hết dữ liệu trước khi thoát (kể cả pendingT25 nhưng d3 chưa có)
        return OpenTrade(t, signalBar, bars[d0], entry, stop, target);
    }

    private static bool IsLocked(IReadOnlyList<Bar> bars, int i, MarketRules rules)
        => i > 0 && rules.IsFloorLocked(bars[i].High, bars[i - 1].Close);

    private static SimulatedTrade OpenTrade(int t, Bar signal, Bar d0, decimal entry, decimal stop, decimal target)
        => new(t, signal.Date, d0.Date, entry, stop, target, null, null, null, null, false, null);

    private static SimulatedTrade Close(int t, Bar signal, Bar d0, decimal entry, decimal stop, decimal target,
        Bar exitBar, decimal exitPrice, string reason, decimal oneR)
    {
        var net = exitPrice * (1m - FeeRate - SellTax - Slippage) - entry * (1m + FeeRate + Slippage);
        return new SimulatedTrade(t, signal.Date, d0.Date, entry, stop, target, exitBar.Date, exitPrice, reason, net / oneR, false, null);
    }
}
