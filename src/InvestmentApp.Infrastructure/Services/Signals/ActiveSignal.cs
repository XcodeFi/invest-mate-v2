namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Kế hoạch của tín hiệu đang kích hoạt. <see cref="Status"/>: "moi" (chưa tới phiên vào lệnh) hoặc "dang-mo" (đã vào, chưa thoát).</summary>
public sealed record ActivePlan(
    DateTime SignalDate, decimal Entry, bool EntryIsEstimate, decimal Stop, decimal Target, string Status, int? DayInTrade);

public static class ActiveSignal
{
    /// <summary>Cửa sổ "đang kích hoạt": tín hiệu trong 3 phiên cuối (t ≥ phiên cuối − 2).</summary>
    public const int WindowSessions = 3;

    /// <summary>
    /// Tín hiệu MỚI NHẤT trong cửa sổ mà lệnh của nó không bị bỏ qua, chưa đóng và không bị bỏ vì chồng lệnh.
    /// Chưa có phiên vào lệnh (d0): vào lệnh là ước tính = đóng cửa t. Có rồi: vào lệnh là giá mở d0, kèm số ngày giữ (d0 là ngày 1).
    /// Cắt lỗ = vào − 2 × ATR14(t); mục tiêu = vào + 2R.
    /// </summary>
    public static ActivePlan? Find(
        IReadOnlyList<Bar> bars, IReadOnlyList<decimal?> atr, IReadOnlyList<int> signals, IReadOnlyList<SimulatedTrade> trades,
        MarketRules rules)
    {
        if (bars.Count == 0) return null;
        var last = bars.Count - 1;

        foreach (var t in signals.Where(s => s >= last - (WindowSessions - 1)).OrderByDescending(s => s))
        {
            var trade = trades.FirstOrDefault(x => x.SignalIndex == t);
            if (trade != null)
            {
                if (trade.Skipped || trade.Completed) continue;
                var day = last - (t + 1) + 1;   // d0 = t + 1 là ngày thứ 1
                return new ActivePlan(trade.SignalDate, trade.Entry!.Value, false, trade.Stop!.Value, trade.Target!.Value, "dang-mo", day);
            }

            // Không có lệnh: hoặc chưa có d0 (tín hiệu ở phiên cuối), hoặc bị bỏ vì chồng lệnh
            if (t + 1 <= last) continue;                                   // có d0 mà không có lệnh = bị bỏ vì chồng lệnh
            if (trades.Any(x => !x.Skipped && x.SignalIndex < t && x.IsOpen)) continue;   // đang giữ lệnh khác
            if (atr[t] is not { } a || a <= 0m) continue;
            // Cùng luật với mô phỏng: phiên tín hiệu đóng ở trần/sàn thì bỏ qua lệnh (Simulate không chạy tới đây vì chưa có d0)
            if (t > 0 && rules.IsAtLimit(bars[t].Close, bars[t - 1].Close)) continue;

            var entry = bars[t].Close;
            var stop = entry - 2m * a;
            if (stop <= 0m) continue;
            return new ActivePlan(bars[t].Date, entry, true, stop, entry + 2m * (entry - stop), "moi", null);
        }

        return null;
    }
}
