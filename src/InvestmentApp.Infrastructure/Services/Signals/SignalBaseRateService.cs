using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>
/// Tỉ lệ nền của setup Pullback trên rổ VN30: chạy đúng engine S1 trên chuỗi giá đã điều chỉnh ĐÃ LƯU của từng mã (không gọi nguồn giá),
/// gộp các lệnh đã đóng của mọi mã đủ điều kiện (đã điều chỉnh, từ 250 nến, còn mới) rồi thống kê bằng <see cref="BaseRateStatistics"/>.
/// Rổ VN30 chỉ gồm mã HOSE nên dùng biên độ HOSE cho cả rổ (không phải tra sàn từng mã: mỗi lần tra là một lần gọi nguồn).
/// </summary>
public class SignalBaseRateService : ISignalBaseRateService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly TimeSpan MarketCloseVietnam = new(15, 10, 0);
    internal static readonly TimeSpan InsufficientCacheTtl = TimeSpan.FromMinutes(30);
    internal static readonly MarketRules BasketRules = MarketRules.For("HOSE");

    private readonly IAdjustedPriceSeriesStore _store;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;

    public SignalBaseRateService(IAdjustedPriceSeriesStore store, IMemoryCache cache, TimeProvider? timeProvider = null)
    {
        _store = store;
        _cache = cache;
        _time = timeProvider ?? TimeProvider.System;
    }

    public static readonly string[] Notes =
    {
        "Khoảng 95% là xấp xỉ: lấy mẫu lại theo khối tháng (2.000 lần, hạt giống cố định); các lệnh có thể kéo dài qua nhiều tháng và cùng chịu một bối cảnh thị trường, nên khoảng có thể vẫn hẹp hơn thực tế.",
        "Tham khảo cho setup trên rổ VN30 hiện tại — không phải bằng chứng riêng của mã này; số liệu mô tả quá khứ.",
        "Rổ hiện tại gồm các mã còn niêm yết (thiên lệch người sống sót); giá đã điều chỉnh theo sự kiện quyền."
    };


    /// <summary>Kết quả gộp: <see cref="Stats"/> null thì <see cref="Reason"/> nói vì sao chưa đủ. K, N, EffectiveMonths luôn có (để thấy cách xa ngưỡng bao nhiêu).</summary>
    public sealed record PooledOutcome(int K, int N, int EffectiveMonths, DateTime? AsOf, BaseRateStats? Stats, string? Reason);

    /// <summary>
    /// Hàm thuần: gộp lệnh đã đóng của các chuỗi đã lưu (null = mã chưa có bản lưu) rồi thống kê. Dùng chung cho dịch vụ và công cụ đếm chỉ đọc,
    /// để con số công cụ in ra đúng là con số người dùng thấy.
    /// </summary>
    public static PooledOutcome Pool(IEnumerable<AdjustedPriceSeriesSnapshot?> snapshots, DateTimeOffset now)
    {
        var vnDay = (now + VietnamOffset).Date;
        var trades = new List<PooledTrade>();
        var used = 0;
        DateTime? asOf = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot == null) continue;

            // Cùng luật với S1: bỏ nến hôm nay còn đang hình thành (trước 15:00 giờ VN); nến cuối quá cũ thì mã không dùng được
            var bars = FormingBar.DropIfForming(snapshot.Bars.OrderBy(b => b.Date).ToList(), b => b.Date, now);
            if (bars.Count < SignalSetupService.MinBars || bars.Any(b => !b.IsAdjusted)) continue;
            var last = bars[^1].Date.Date;
            if ((vnDay - last).TotalDays > SignalSetupService.MaxStaleCalendarDays) continue;

            var series = bars.Select(b => new Bar(b.Date, b.Open, b.High, b.Low, b.Close)).ToList();
            var result = PullbackBacktest.Run(series, BasketRules);
            foreach (var t in result.Trades)
                if (t.Completed && t.R != null && t.EntryDate != null)
                    trades.Add(new PooledTrade(t.EntryDate.Value, t.R.Value));

            used++;
            if (asOf == null || last > asOf) asOf = last;
        }

        var effective = BaseRateStatistics.CountEffectiveMonths(trades);
        var reason = BaseRateStatistics.GateFailure(used, trades.Count, effective);
        var stats = reason == null ? BaseRateStatistics.Compute(used, trades) : null;
        return new PooledOutcome(used, trades.Count, effective, asOf, stats, reason);
    }

    private sealed record Cached(SignalBaseRateResult Result, DateTimeOffset Until);

    public async Task<SignalBaseRateResult> GetPullbackBaseRateAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var vnDay = (now + VietnamOffset).Date;
        var key = $"signals:baserate:pullback:{vnDay:yyyyMMdd}";
        if (_cache.TryGetValue(key, out Cached? cached) && cached != null && now < cached.Until)
            return cached.Result;

        var snapshots = new List<AdjustedPriceSeriesSnapshot?>();
        foreach (var symbol in Vn30Basket.Symbols)
            snapshots.Add(await _store.GetAsync(symbol, cancellationToken));

        var pooled = Pool(snapshots, now);
        var outcome = pooled.Stats == null
            ? new SignalBaseRateResult(null, pooled.Reason)
            : new SignalBaseRateResult(new SignalBaseRateDto
            {
                K = pooled.Stats.K, N = pooled.Stats.N, EffectiveMonths = pooled.Stats.EffectiveMonths,
                WinRate = pooled.Stats.WinRate, WinRateLow = pooled.Stats.WinRateLow, WinRateHigh = pooled.Stats.WinRateHigh,
                EvR = pooled.Stats.EvR, EvRLow = pooled.Stats.EvRLow, EvRHigh = pooled.Stats.EvRHigh,
                AsOf = pooled.AsOf!.Value,
                Notes = Notes.ToList()
            }, null);

        // Kết quả tính trước 15:10 chỉ giữ tới 15:10 (nến hôm nay có thể vừa đóng); sau đó giữ tới hết ngày. Chuỗi lưu thay đổi dần theo warm-up,
        // nên kết quả "chưa đủ" cũng chỉ giữ tới mốc đó chứ không ghim cả ngày hơn thế.
        var close = new DateTimeOffset(vnDay + MarketCloseVietnam, VietnamOffset);
        var until = now < close ? close : new DateTimeOffset(vnDay.AddDays(1), VietnamOffset);
        // Warm-up thêm khoảng một mã mỗi lượt, nên "chưa đủ" chỉ giữ ngắn để số k/30 không chậm cả ngày.
        if (outcome.BaseRate == null)
        {
            var soon = now + InsufficientCacheTtl;
            if (soon < until) until = soon;
        }
        _cache.Set(key, new Cached(outcome, until), until.AddDays(1));
        return outcome;
    }
}
