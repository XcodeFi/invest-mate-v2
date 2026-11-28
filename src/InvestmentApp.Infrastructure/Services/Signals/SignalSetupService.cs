using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>
/// Lịch sử setup Pullback trên một mã: lấy chuỗi giá ngày đã điều chỉnh, kiểm chất lượng dữ liệu, chạy <see cref="PullbackBacktest"/>,
/// tính thống kê (chỉ khi đủ 30 lệnh đã đóng) và tín hiệu đang kích hoạt. Không có danh sách từng lệnh trong kết quả.
/// </summary>
public class SignalSetupService : ISignalSetupService
{
    public const string UnavailableReason = "Chưa đủ lịch sử giá đã điều chỉnh để thống kê";

    /// <summary>Số nến tối thiểu để thống kê (≈ 1 năm giao dịch). Mã mới lên sàn chưa đủ thì không có số liệu.</summary>
    public const int MinBars = 250;

    /// <summary>Nến cuối cũ hơn bấy nhiêu NGÀY LỊCH (giờ Việt Nam) thì coi là cũ. Ngày lịch, không phải ngày làm việc: nghỉ Tết liền vài ngày vẫn bình thường.</summary>
    public const int MaxStaleCalendarDays = 10;

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IMarketDataProvider _prices;
    private readonly IStockInfoProvider _info;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<SignalSetupService>? _logger;

    public SignalSetupService(
        IMarketDataProvider prices, IStockInfoProvider info, IMemoryCache cache,
        TimeProvider? timeProvider = null, ILogger<SignalSetupService>? logger = null)
    {
        _prices = prices;
        _info = info;
        _cache = cache;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    private DateTime VietnamToday => (_time.GetUtcNow() + VietnamOffset).Date;

    public async Task<SignalSetupsDto> GetSignalSetupsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpperInvariant().Trim();
        var today = VietnamToday;
        var key = $"signals:{symbol}:{today:yyyyMMdd}";
        if (_cache.TryGetValue(key, out CachedSetups? cached) && cached != null && _time.GetUtcNow() < cached.Until)
            return cached.Dto;

        var prices = await _prices.GetHistoricalPricesAsync(symbol, today.AddYears(-4), today, cancellationToken);
        var bars = prices.OrderBy(p => p.Date).ToList();

        var dto = new SignalSetupsDto
        {
            Symbol = symbol,
            Adjusted = bars.Count > 0 && bars.All(b => b.IsAdjusted),
            AdjustedAsOf = bars.Select(b => b.AdjustedAsOf).LastOrDefault(d => d != null)
        };

        var unavailable = bars.Count < MinBars
                          || !dto.Adjusted
                          || (today - bars[^1].Date.Date).TotalDays > MaxStaleCalendarDays;
        if (unavailable)
        {
            dto.ReasonUnavailable = UnavailableReason;
            return dto;   // không nhớ kết quả "không có dữ liệu": nguồn có thể vừa lỗi tạm thời
        }

        var (rules, exchangeKnown) = await ResolveRulesWithCertaintyAsync(symbol, cancellationToken);
        var series = bars.Select(b => new Bar(b.Date, b.Open, b.High, b.Low, b.Close)).ToList();
        dto.Setups.Add(BuildPullback(series, rules));
        dto.Notes.AddRange(Notes);

        // Không ghim cả ngày một kết quả tính bằng luật dự phòng 7% khi chưa biết sàn (HNX/UPCOM sẽ giữ số sai tới hết ngày): giữ vài phút.
        // Và không phục vụ sau 15:10 một kết quả tính trước giờ đóng cửa: nguồn lấy lại chuỗi một lần sau đó, phiên hôm nay có thể tạo tín hiệu mới.
        var until = exchangeKnown ? GoodUntil() : _time.GetUtcNow().AddMinutes(FallbackCacheMinutes);
        _cache.Set(key, new CachedSetups(dto, until), until.AddDays(1));
        return dto;
    }

    private const int FallbackCacheMinutes = 5;
    private static readonly TimeSpan MarketCloseVietnam = new(15, 10, 0);

    private sealed record CachedSetups(SignalSetupsDto Dto, DateTimeOffset Until);

    /// <summary>Hạn dùng của kết quả tính lúc này: 15:10 giờ VN nếu tính trước đó (cùng mốc với chuỗi giá của nguồn), không thì hết ngày VN.</summary>
    private DateTimeOffset GoodUntil()
    {
        var now = _time.GetUtcNow();
        var vnDay = VietnamToday;
        var close = new DateTimeOffset(vnDay + MarketCloseVietnam, VietnamOffset);
        return now < close ? close : new DateTimeOffset(vnDay.AddDays(1), VietnamOffset);
    }

    internal static readonly string[] Notes =
    {
        "Giá đã điều chỉnh theo sự kiện quyền (cổ tức, thưởng, chia tách), nên mức giá cũ khác mức giao dịch thật lúc đó.",
        "Chỉ có các mã còn niêm yết: mã đã hủy niêm yết không nằm trong dữ liệu (thiên lệch người sống sót).",
        "HOSE không có lệnh dừng; mô phỏng khớp đúng giá cắt lỗ là lạc quan hơn thực tế.",
        "T+2,5 được mô phỏng thận trọng: chạm cắt lỗ hoặc mục tiêu trong 3 phiên đầu thì thoát ở giá mở phiên thứ 4, không phải chiều phiên thứ 3.",
        "Đã trừ phí 0,15% mỗi chiều, thuế bán 0,1% và trượt giá 0,1% mỗi chiều."
    };

    private static SignalSetupDto BuildPullback(List<Bar> bars, MarketRules rules)
    {
        var series = IndicatorSeries.Build(bars);
        var signals = PullbackSetup.FindSignals(bars, series);
        var trades = SetupBacktester.Simulate(bars, signals, series.Atr14, rules);
        var summary = SetupBacktester.Summarize(trades);

        var rs = trades.Where(t => t.R != null).Select(t => t.R!.Value).ToList();
        var stats = SetupStatistics.Compute(rs);
        var active = ActiveSignal.Find(bars, series.Atr14, signals, trades, rules);

        return new SignalSetupDto
        {
            Key = "pullback",
            Name = "Pullback xu hướng",
            Description = "Trong xu hướng tăng (EMA20 trên EMA50, ADX từ 20), giá hồi về sát EMA20 rồi đóng cửa trên EMA20 khi RSI nằm trong 40–55.",
            Signals = signals.Count,
            Completed = summary.Completed,
            Open = summary.Open,
            Skipped = summary.Skipped,
            N = stats.N,
            MinSample = SetupStatistics.MinSample,
            SampleSufficient = stats.Sufficient,
            WinRate = stats.WinRate,
            WinRateLow = stats.WinRateLow,
            WinRateHigh = stats.WinRateHigh,
            EvR = stats.EvR,
            EvRLow = stats.EvRLow,
            EvRHigh = stats.EvRHigh,
            AvgWinR = stats.AvgWinR,
            AvgLossR = stats.AvgLossR,
            ActiveSince = active?.SignalDate,
            ActivePlan = active == null ? null : new SignalActivePlanDto
            {
                SignalDate = active.SignalDate, Entry = active.Entry, EntryIsEstimate = active.EntryIsEstimate,
                Stop = active.Stop, Target = active.Target, Status = active.Status, DayInTrade = active.DayInTrade
            }
        };
    }

    /// <summary>
    /// Luật giá theo sàn của mã. Tra sàn qua chi tiết cổ phiếu (một lần mỗi mã mỗi ngày, nguồn nhẹ nhất đang có);
    /// thiếu, lạ hoặc tra lỗi thì 7% (giả định chặt nhất). Kết quả tra lỗi không được nhớ.
    /// </summary>
    internal async Task<MarketRules> ResolveRulesAsync(string symbol, CancellationToken cancellationToken)
        => (await ResolveRulesWithCertaintyAsync(symbol, cancellationToken)).Rules;

    /// <summary><c>Known</c> = false khi không tra được sàn (lỗi, thiếu): luật 7% chỉ là dự phòng, kết quả tính bằng nó không được nhớ cả ngày.</summary>
    internal async Task<(MarketRules Rules, bool Known)> ResolveRulesWithCertaintyAsync(string symbol, CancellationToken cancellationToken)
    {
        var key = $"signals:exchange:{symbol}:{VietnamToday:yyyyMMdd}";
        if (_cache.TryGetValue(key, out string? known))
            return (MarketRules.For(known), true);

        try
        {
            var detail = await _info.GetStockDetailAsync(symbol, cancellationToken);
            var exchange = detail?.Exchange;
            if (string.IsNullOrWhiteSpace(exchange))
                return (MarketRules.For(null), false);

            _cache.Set(key, exchange, new DateTimeOffset(VietnamToday.AddDays(1), VietnamOffset));
            return (MarketRules.For(exchange), true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Exchange lookup for {Symbol} failed; using the 7% default limit", symbol);
            return (MarketRules.For(null), false);
        }
    }
}
