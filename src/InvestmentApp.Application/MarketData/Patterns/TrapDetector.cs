using System.Globalization;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Patterns;

/// <summary>
/// Ngưỡng của bộ dò bẫy. Mọi giá trị là mặc định có thể chỉnh, không có nguồn nào kiểm chứng: cửa sổ 20 phiên (kênh Donchian 20) là quy ước phổ biến,
/// 5 phiên xác nhận và 20 phiên "nghỉ" giữa hai mốc cùng loại là lựa chọn của thiết kế; cắt lỗ quy ước là đáy 10 phiên (phá vỡ) hoặc đáy đợt thủng giả
/// (bẫy giảm giá), trừ thêm 1%.
/// </summary>
public sealed class TrapOptions
{
    public int LookbackSessions { get; init; } = 20;
    public int ConfirmSessions { get; init; } = 5;
    public int CooldownSessions { get; init; } = 20;
    public int StopLookbackSessions { get; init; } = 10;
    public decimal StopFactor { get; init; } = 0.99m;
    /// <summary>Chỉ trả về bấy nhiêu mốc gần nhất (cũ trước).</summary>
    public int MaxEvents { get; init; } = 12;
}

/// <summary>
/// Một mốc bẫy xác nhận HỒI TỐ: nến phá vỡ (hoặc thủng) ở phiên t, giá đóng cửa quay lại phía bên kia mức trong vòng N phiên, và mốc chỉ hiện khi đã qua đủ t+N.
/// Cố ý KHÔNG có tần suất, tỉ lệ hay xác suất: đây là thống kê thuộc PR khác. Các số % là khoảng cách giá của CHÍNH mốc này.
/// </summary>
public sealed class TrapEvent
{
    /// <summary>failed-breakout (phá vỡ thất bại, bẫy tăng giá) | bear-trap (bẫy giảm giá).</summary>
    public string Type { get; init; } = "";
    public string TypeVi { get; init; } = "";
    /// <summary>Phiên t: nến phá vỡ lên trên đỉnh 20 phiên (failed-breakout) hoặc thủng đáy 20 phiên (bear-trap). Mốc vẽ lên biểu đồ.</summary>
    public DateTime BreakoutDate { get; init; }
    /// <summary>Phiên đầu tiên giá đóng cửa quay lại phía bên kia mức.</summary>
    public DateTime ReturnDate { get; init; }
    /// <summary>Phiên t+N: từ đây mốc mới được xác nhận (không có mốc nào cho các phiên gần nhất).</summary>
    public DateTime ConfirmedDate { get; init; }
    /// <summary>Pivot (đỉnh 20 phiên trước t) hoặc hỗ trợ (đáy 20 phiên trước t).</summary>
    public decimal LevelPrice { get; init; }
    /// <summary>Cắt lỗ quy ước của lệnh theo hướng phá vỡ: dưới đáy 10 phiên trước t (failed-breakout) hoặc dưới đáy đợt thủng giả (bear-trap).</summary>
    public decimal StopPrice { get; init; }
    /// <summary>Nếu sai: % từ mức tới cắt lỗ quy ước (số dương, hiển thị "−X%").</summary>
    public decimal RiskPct { get; init; }
    /// <summary>Đo được trong cửa sổ: % giá đi xuyên qua mức (thấp nhất của t+1..t+N dưới pivot, hoặc đáy đợt thủng dưới hỗ trợ).</summary>
    public decimal ExcursionPct { get; init; }
    public string Description { get; init; } = "";
    public string Disclaimer { get; init; } = "";
}

/// <summary>
/// Dò bẫy trên chuỗi giá ngày đã điều chỉnh. Hàm THUẦN: không đọc đồng hồ, không đổi đầu vào, không phụ thuộc thứ tự đầu vào.
/// <list type="bullet">
/// <item>Phá vỡ thất bại: phiên t đóng cửa trên đỉnh cao nhất của 20 phiên trước (nến phá vỡ ĐẦU TIÊN), rồi trong t+1..t+N có một phiên đóng cửa dưới pivot đó.</item>
/// <item>Bẫy giảm giá: phiên t đóng cửa dưới đáy thấp nhất của 20 phiên trước, rồi trong t+1..t+N có một phiên đóng cửa lại trên hỗ trợ đó.</item>
/// </list>
/// Chỉ báo mốc khi cửa sổ đã trọn (t+N ≤ nến cuối): không dự đoán. Hai mốc cùng loại cách nhau ít nhất <see cref="TrapOptions.CooldownSessions"/> phiên.
/// </summary>
public static class TrapDetector
{
    public static List<TrapEvent> Detect(IReadOnlyList<StockPriceData> input, TrapOptions? options = null)
    {
        var o = options ?? new TrapOptions();
        var bars = input.OrderBy(b => b.Date).ToList();
        var n = bars.Count;
        var events = new List<TrapEvent>();
        var lastBreakout = int.MinValue / 2;
        var lastBear = int.MinValue / 2;

        for (var t = o.LookbackSessions + 1; t + o.ConfirmSessions <= n - 1; t++)
        {
            // ----- phá vỡ thất bại
            var pivot = MaxHigh(bars, t - o.LookbackSessions, t - 1);
            var pivotPrev = MaxHigh(bars, t - 1 - o.LookbackSessions, t - 2);
            if (bars[t].Close > pivot && !(bars[t - 1].Close > pivotPrev) && t - lastBreakout >= o.CooldownSessions)
            {
                var f = FirstIndex(bars, t + 1, t + o.ConfirmSessions, b => b.Close < pivot);
                if (f >= 0)
                {
                    var stop = MinLow(bars, Math.Max(0, t - o.StopLookbackSessions), t - 1) * o.StopFactor;
                    var worst = MinLow(bars, t + 1, t + o.ConfirmSessions);
                    events.Add(new TrapEvent
                    {
                        Type = "failed-breakout", TypeVi = "Phá vỡ thất bại",
                        BreakoutDate = bars[t].Date, ReturnDate = bars[f].Date, ConfirmedDate = bars[t + o.ConfirmSessions].Date,
                        LevelPrice = pivot, StopPrice = stop,
                        RiskPct = Round1(Math.Max(0m, (pivot - stop) / pivot * 100m)),
                        ExcursionPct = Round1(Math.Max(0m, (pivot - worst) / pivot * 100m)),
                        Description = $"Giá đóng cửa vượt đỉnh {o.LookbackSessions} phiên (pivot {Fmt(pivot)}) ngày {Day(bars[t].Date)} nhưng đã đóng cửa trở lại dưới pivot ngày {Day(bars[f].Date)}.",
                        Disclaimer = Disclaimer(o)
                    });
                    lastBreakout = t;
                }
            }

            // ----- bẫy giảm giá
            var support = MinLow(bars, t - o.LookbackSessions, t - 1);
            var supportPrev = MinLow(bars, t - 1 - o.LookbackSessions, t - 2);
            if (bars[t].Close < support && !(bars[t - 1].Close < supportPrev) && t - lastBear >= o.CooldownSessions)
            {
                var r = FirstIndex(bars, t + 1, t + o.ConfirmSessions, b => b.Close > support);
                if (r >= 0)
                {
                    var falseBreakLow = MinLow(bars, t, r);
                    var stop = falseBreakLow * o.StopFactor;
                    events.Add(new TrapEvent
                    {
                        Type = "bear-trap", TypeVi = "Bẫy giảm giá",
                        BreakoutDate = bars[t].Date, ReturnDate = bars[r].Date, ConfirmedDate = bars[t + o.ConfirmSessions].Date,
                        LevelPrice = support, StopPrice = stop,
                        RiskPct = Round1(Math.Max(0m, (support - stop) / support * 100m)),
                        ExcursionPct = Round1(Math.Max(0m, (support - falseBreakLow) / support * 100m)),
                        Description = $"Giá đóng cửa thủng đáy {o.LookbackSessions} phiên (hỗ trợ {Fmt(support)}) ngày {Day(bars[t].Date)} nhưng đã đóng cửa trở lại trên hỗ trợ ngày {Day(bars[r].Date)}.",
                        Disclaimer = Disclaimer(o)
                    });
                    lastBear = t;
                }
            }
        }

        return events.OrderBy(e => e.BreakoutDate).ThenBy(e => e.Type, StringComparer.Ordinal).TakeLast(o.MaxEvents).ToList();
    }

    private static string Disclaimer(TrapOptions o) =>
        $"Mốc xác nhận hồi tố sau {o.ConfirmSessions} phiên, không phải dự báo và không có thống kê tần suất. \"Nếu sai\" là khoảng cách tới cắt lỗ quy ước, không phải mức cắt lỗ khuyến nghị.";

    private static decimal Round1(decimal v) => Math.Round(v, 1);
    private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Day(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

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

    private static int FirstIndex(List<StockPriceData> bars, int from, int to, Func<StockPriceData, bool> match)
    {
        for (var k = from; k <= to; k++)
            if (match(bars[k])) return k;
        return -1;
    }
}
