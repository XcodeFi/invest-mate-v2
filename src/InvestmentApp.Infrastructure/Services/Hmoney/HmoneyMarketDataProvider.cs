using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

public class MarketDataProviderOptions
{
    public string BaseUrl { get; set; } = "https://api-finance-t19.24hmoney.vn";
    public int TimeoutSeconds { get; set; } = 15;
    public int PriceCacheTtlSeconds { get; set; } = 15;
    public int IndexCacheTtlSeconds { get; set; } = 15;
    public int CompanyListCacheTtlMinutes { get; set; } = 30;

    /// <summary>
    /// Host của UDF (TradingView) mà biểu đồ web 24hmoney dùng: nến ngày thật, đã điều chỉnh theo sự kiện
    /// quyền, từ 2017. API không công bố nên có dự phòng sang endpoint graph. URL công khai, không phải secret.
    /// </summary>
    public string TradingViewBaseUrl { get; set; } = "https://24hmoney.vn";

    /// <summary>Số năm lịch sử ngày xin từ UDF (3 năm ≈ 750 phiên, đủ cho EMA200 có thời gian hội tụ).</summary>
    public int TradingViewHistoryYears { get; set; } = 3;

    /// <summary>Sau một lần UDF lỗi cho một mã, không gọi lại UDF cho mã đó trong chừng này phút (không thử lại theo vòng).</summary>
    public int TradingViewFailureCooldownMinutes { get; set; } = 30;

    /// <summary>Số lần lỗi LIÊN TIẾP (bất kể mã) để coi UDF đang chặn cả IP và ngừng gọi cho mọi mã.</summary>
    public int TradingViewGlobalFailureThreshold { get; set; } = 3;

    /// <summary>Thời gian ngừng gọi UDF cho mọi mã sau khi chạm ngưỡng lỗi liên tiếp.</summary>
    public int TradingViewGlobalCooldownMinutes { get; set; } = 15;
}

/// <summary>
/// Market data provider using 24hmoney.vn public APIs.
/// - Prices are scaled ×1000 (API returns prices in units of 1,000 VND)
/// - Results are cached in IMemoryCache with configurable TTL
/// </summary>
public class HmoneyMarketDataProvider : IMarketDataProvider, IStockInfoProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HmoneyMarketDataProvider> _logger;
    private readonly MarketDataProviderOptions _options;
    private readonly IAdjustedPriceSeriesStore? _store;
    private readonly TimeProvider _time;

    // Mỗi mã một cổng: nhiều yêu cầu đồng thời cho cùng mã chỉ gây đúng một lần gọi UDF.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SymbolGates = new();

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly TimeSpan PersistedReadCache = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FallbackCache = TimeSpan.FromSeconds(60);

    private sealed record Timed<T>(T Value, DateTimeOffset Until);

    /// <summary>Số lần UDF lỗi liên tiếp (dùng chung mọi mã trong cùng một bộ nhớ đệm).</summary>
    private sealed class FailureStreak { public int Count; }

    // 24hmoney returns stock prices in units of 1,000 VND
    private const decimal PriceScale = 1000m;

    /// <summary>Tham số <c>type</c> duy nhất của endpoint graph trả lợi suất theo phiên.</summary>
    private const int DailyGraphType = 3;
    private const int IntradayGraphType = 1;

    /// <summary>
    /// Trường <c>z</c> của graph ngày bằng 1/10 số cổ phiếu: đo 2026-11-28, tỉ số v(UDF)/z là 10,00–10,10 trên
    /// 61/61 phiên của cả FPT và VNM, và v ngày 2026-11-27 = accumulated_vol của trading-history. Nhân 10 để
    /// khối lượng của nguồn dự phòng cùng đơn vị (cổ phiếu) với nguồn UDF, nếu không OBV/MFI/KL TB nhảy bậc khi đổi nguồn.
    /// </summary>
    private const int GraphDailyVolumeFactor = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CommonParams =
        "device_id=web&locale=vi&device_name=INVALID&device_model=INVALID" +
        "&network_carrier=INVALID&connection_type=INVALID&os=Chrome&os_version=1" +
        "&access_token=INVALID&push_token=INVALID";

    public HmoneyMarketDataProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<HmoneyMarketDataProvider> logger,
        IOptions<MarketDataProviderOptions> options,
        IAdjustedPriceSeriesStore? store = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    private static decimal ScalePrice(decimal raw) => raw * PriceScale;

    // =============================================
    // IMarketDataProvider implementation
    // =============================================

    public async Task<StockPriceData?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();
        var detail = await GetRawShareDetailAsync(symbol, cancellationToken);
        if (detail == null)
            return null;

        var price = detail.MatchPrice > 0 ? detail.MatchPrice : detail.Price;

        return new StockPriceData
        {
            Symbol = detail.Symbol ?? symbol,
            Date = DateTime.UtcNow.Date,
            Open = ScalePrice(detail.OpenPrice),
            High = ScalePrice(detail.HighestPrice),
            Low = ScalePrice(detail.LowestPrice),
            Close = ScalePrice(price),
            Volume = (long)detail.AccumulatedVol
        };
    }

    public async Task<List<StockPriceData>> GetHistoricalPricesAsync(
        string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();

        // Khoảng ≤ 1 ngày là intraday: giữ nguyên nguồn graph. Từ 2 ngày trở lên là lịch sử NGÀY: lấy nến
        // thật đã điều chỉnh từ UDF, lỗi thì dự phòng sang graph (chỉ có giá đóng cửa, chưa điều chỉnh).
        if ((to - from).TotalDays > 1)
            return await GetDailyBarsAsync(symbol, from, to, cancellationToken);

        return await GetGraphHistoryAsync(symbol, from, to, cancellationToken);
    }

    private async Task<List<StockPriceData>> GetDailyBarsAsync(
        string symbol, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var series = await GetDailySeriesAsync(symbol, cancellationToken);
        return series
            .Where(p => p.Date >= from.Date && p.Date <= to.Date)
            .ToList();
    }

    private DateTimeOffset Now => _time.GetUtcNow();
    private DateTime VietnamDate(DateTimeOffset utc) => (utc + VietnamOffset).Date;

    private bool TryFresh<T>(string key, out T value)
    {
        if (_cache.TryGetValue(key, out Timed<T>? entry) && entry != null && Now < entry.Until)
        {
            value = entry.Value;
            return true;
        }
        value = default!;
        return false;
    }

    private void SetTimed<T>(string key, T value, DateTimeOffset until) =>
        _cache.Set(key, new Timed<T>(value, until), TimeSpan.FromHours(26));   // hạn thật do `Until` quyết, theo đồng hồ của provider

    /// <summary>
    /// Sau giờ này (giờ Việt Nam) nến của phiên hôm nay đã chốt. Chuỗi lấy TRƯỚC giờ này thiếu hoặc còn dở nến hôm nay,
    /// nên được phép lấy lại đúng một lần sau giờ đóng cửa (ngân sách tối đa 2 lần mỗi mã mỗi ngày).
    /// </summary>
    private static readonly TimeSpan MarketCloseVietnam = new(15, 10, 0);

    /// <summary>Hạn dùng của chuỗi tốt lấy lúc <paramref name="fetchedAtUtc"/>: tới giờ đóng cửa nếu lấy trước đó, không thì tới hết ngày.</summary>
    private DateTimeOffset GoodUntil(DateTimeOffset fetchedAtUtc)
    {
        var vnDay = VietnamDate(fetchedAtUtc);
        var close = new DateTimeOffset(vnDay + MarketCloseVietnam, VietnamOffset);
        return fetchedAtUtc < close ? close : new DateTimeOffset(vnDay.AddDays(1), VietnamOffset);
    }

    /// <summary>Chuỗi lấy lúc <paramref name="fetchedAtUtc"/> còn dùng được lúc này không (cùng ngày Việt Nam và chưa qua mốc đóng cửa kể từ lúc lấy).</summary>
    private bool IsFresh(DateTimeOffset fetchedAtUtc) => Now < GoodUntil(fetchedAtUtc) && VietnamDate(fetchedAtUtc) == VietnamDate(Now);

    /// <summary>
    /// Thứ tự đọc (chuỗi CHƯA lọc theo khoảng): bộ nhớ đệm chuỗi tốt trong ngày → bản đã lưu bền nếu lấy trong
    /// hôm nay → UDF (nếu không đang nghỉ) → bản đã lưu bền cũ hơn → nguồn graph cũ (<c>IsAdjusted=false</c>).
    /// UDF tối đa một lần mỗi mã mỗi ngày; lỗi thì mã đó nghỉ, nhiều mã lỗi liên tiếp thì cả UDF nghỉ. Không thử lại theo vòng.
    /// Chuỗi graph dự phòng nằm ở khoá riêng nên KHÔNG BAO GIỜ ghi đè một chuỗi tốt.
    /// </summary>
    private async Task<List<StockPriceData>> GetDailySeriesAsync(string symbol, CancellationToken cancellationToken)
    {
        var goodKey = $"tv:good:{symbol}";
        if (TryFresh(goodKey, out List<StockPriceData> good))
            return good;

        var gate = SymbolGates.GetOrAdd(symbol, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryFresh(goodKey, out good))
                return good;

            // Bản lưu bền cũng là "bộ nhớ đệm": lấy trong hôm nay thì không gọi UDF nữa, kể cả sau khi khởi động lại.
            var snapshot = await ReadSnapshotAsync(symbol, cancellationToken);
            if (snapshot != null && IsFresh(new DateTimeOffset(snapshot.FetchedAtUtc, TimeSpan.Zero)))
            {
                var bars = snapshot.Bars.ToList();
                SetTimed(goodKey, bars, GoodUntil(new DateTimeOffset(snapshot.FetchedAtUtc, TimeSpan.Zero)));
                return bars;
            }

            if (!UdfCooledDown(symbol))
            {
                var fetchedAt = Now;
                var result = await FetchTradingViewAsync(symbol, cancellationToken);
                if (result.Bars != null)
                {
                    RecordUdfSuccess();
                    SetTimed(goodKey, result.Bars, GoodUntil(fetchedAt));
                    await SaveSnapshotAsync(symbol, result.Bars, cancellationToken);
                    return result.Bars;
                }

                if (result.NoData)
                    RecordUdfNoData(symbol);   // mã nguồn không có (ví dụ chỉ số): không phải bị chặn nên không tính vào chuỗi lỗi
                else
                    RecordUdfFailure(symbol);
            }

            if (snapshot != null)
            {
                var ageDays = (VietnamDate(Now) - VietnamDate(snapshot.FetchedAtUtc)).Days;
                if (ageDays >= 2)
                    _logger.LogWarning(
                        "Serving a persisted adjusted series for {Symbol} that is {Days} days old (source unavailable); a newer ex-date may be missing",
                        symbol, ageDays);
                return snapshot.Bars.ToList();
            }

            var fallbackKey = $"tv:fallback:{symbol}";
            if (TryFresh(fallbackKey, out List<StockPriceData> cachedFallback))
                return cachedFallback;

            var graph = await FetchGraphSeriesAsync(symbol, DailyGraphType, cancellationToken);
            if (graph == null || graph.Count == 0)
                return new List<StockPriceData>();   // rỗng thì không đệm: có thể chỉ trục trặc nhất thời

            SetTimed(fallbackKey, graph, Now + FallbackCache);
            return graph;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AdjustedPriceSeriesSnapshot?> ReadSnapshotAsync(string symbol, CancellationToken cancellationToken)
    {
        if (_store == null) return null;

        var key = $"tv:persisted:{symbol}";
        if (TryFresh(key, out AdjustedPriceSeriesSnapshot? cached))
            return cached;

        try
        {
            var snapshot = await _store.GetAsync(symbol, cancellationToken);
            if (snapshot != null)
                SetTimed(key, snapshot, Now + PersistedReadCache);   // tránh đọc Mongo mỗi lần khi UDF đang nghỉ
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read persisted adjusted series for {Symbol}", symbol);
            return null;
        }
    }

    private async Task SaveSnapshotAsync(string symbol, List<StockPriceData> bars, CancellationToken cancellationToken)
    {
        if (_store == null) return;
        try
        {
            await _store.SaveAsync(symbol, Now.UtcDateTime, bars, cancellationToken);
            SetTimed($"tv:persisted:{symbol}", new AdjustedPriceSeriesSnapshot(symbol, Now.UtcDateTime, bars), Now + PersistedReadCache);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Lưu hỏng chỉ mất bản dự phòng, không làm hỏng kết quả vừa lấy được.
            _logger.LogWarning(ex, "Failed to persist adjusted series for {Symbol}", symbol);
        }
    }

    /// <summary>Nguồn trả đúng cấu trúc nhưng không có dữ liệu cho mã này: chỉ nghỉ theo mã, không kéo cả nguồn nghỉ chung.</summary>
    private void RecordUdfNoData(string symbol) =>
        SetTimed($"tv:cooldown:{symbol}", true, Now.AddMinutes(_options.TradingViewFailureCooldownMinutes));

    private bool UdfCooledDown(string symbol) =>
        TryFresh("tv:cooldown:global", out bool _) || TryFresh($"tv:cooldown:{symbol}", out bool _);

    private FailureStreak Streak() =>
        _cache.GetOrCreate("tv:fail-streak", e => { e.SetSlidingExpiration(TimeSpan.FromHours(1)); return new FailureStreak(); })!;

    private void RecordUdfSuccess() => Interlocked.Exchange(ref Streak().Count, 0);

    private void RecordUdfFailure(string symbol)
    {
        SetTimed($"tv:cooldown:{symbol}", true, Now.AddMinutes(_options.TradingViewFailureCooldownMinutes));
        var count = Interlocked.Increment(ref Streak().Count);
        if (count >= _options.TradingViewGlobalFailureThreshold)
        {
            SetTimed("tv:cooldown:global", true, Now.AddMinutes(_options.TradingViewGlobalCooldownMinutes));
            _logger.LogWarning(
                "TradingView history failed {Count} times in a row; pausing all calls for {Minutes} minutes",
                count, _options.TradingViewGlobalCooldownMinutes);
        }
    }

    /// <summary>
    /// <c>null</c> khi không dùng được (lỗi mạng, hết giờ, HTTP không thành công, <c>s != "ok"</c>, mảng rỗng
    /// hoặc lệch): người gọi chuyển sang nguồn dự phòng. Huỷ từ người gọi thì ném tiếp, không coi là lỗi nguồn.
    /// </summary>
    private sealed record UdfResult(List<StockPriceData>? Bars, bool NoData);

    private async Task<UdfResult> FetchTradingViewAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var url = $"{_options.TradingViewBaseUrl.TrimEnd('/')}/tradingview/history" +
                      $"?symbol={Uri.EscapeDataString(symbol)}&resolution=D" +
                      $"&from={now.AddYears(-_options.TradingViewHistoryYears).ToUnixTimeSeconds()}" +
                      $"&to={now.AddDays(1).ToUnixTimeSeconds()}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TradingView history for {Symbol} returned HTTP {Status}; falling back to graph (unadjusted)",
                    symbol, (int)response.StatusCode);
                return new UdfResult(null, false);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var bars = TradingViewHistoryParser.Parse(body, symbol);
            if (bars != null)
                return new UdfResult(bars, false);

            var noData = TradingViewHistoryParser.IsNoData(body);
            _logger.LogWarning("TradingView history for {Symbol} was {Kind}; falling back to graph (unadjusted)",
                symbol, noData ? "reported as no_data" : "empty or unreadable");
            return new UdfResult(null, noData);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TradingView history for {Symbol} failed; falling back to graph (unadjusted)", symbol);
            return new UdfResult(null, false);
        }
    }

    private async Task<List<StockPriceData>> GetGraphHistoryAsync(
        string symbol, DateTime from, DateTime to, CancellationToken cancellationToken)
    {

        int graphType = IntradayGraphType;   // chỉ còn nhánh intraday; lịch sử ngày đi qua GetDailyBarsAsync

        var cacheKey = $"history:{symbol}:{graphType}";
        if (!_cache.TryGetValue(cacheKey, out List<StockPriceData>? series))
        {
            series = await FetchGraphSeriesAsync(symbol, graphType, cancellationToken);
            if (series == null || series.Count == 0)
                return new List<StockPriceData>();

            // Historical data changes less frequently — cache 60s.
            // Cache chuỗi CHƯA lọc: mọi khoảng nay dùng chung một cacheKey, nên nếu cache bản đã
            // lọc thì một lần xin hẹp sẽ cắt mất mọi lần xin rộng sau đó. Chuỗi rỗng KHÔNG cache:
            // có thể chỉ là trục trặc nhất thời, cache 60 giây là tự khoá mình khỏi lần thử sau.
            _cache.Set(cacheKey, series, TimeSpan.FromSeconds(60));
        }

        return series!
            .Where(p => p.Date >= from.Date && p.Date <= to.Date)
            .ToList();
    }

    /// <summary>Trả <c>null</c> khi không lấy được; danh sách rỗng nghĩa là nguồn thật sự không có điểm nào.</summary>
    private async Task<List<StockPriceData>?> FetchGraphSeriesAsync(
        string symbol, int graphType, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"/v2/ios/stock/graph?symbol={symbol}&type={graphType}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyGraphData>>(url, JsonOptions, cancellationToken);

            var graphData = response?.Data;
            if (graphData?.Points == null || graphData.Points.Count == 0)
            {
                _logger.LogWarning("No historical graph data returned for {Symbol}", symbol);
                return new List<StockPriceData>();
            }

            return graphData.Points
                .Select(point =>
                {
                    var scaledPrice = ScalePrice(point.Y);
                    return new StockPriceData
                    {
                        Symbol = symbol,
                        Date = DateTimeOffset.FromUnixTimeSeconds(point.X).UtcDateTime.Date,
                        Open = scaledPrice,
                        High = scaledPrice,
                        Low = scaledPrice,
                        Close = scaledPrice,
                        Volume = graphType == DailyGraphType ? point.Z * GraphDailyVolumeFactor : point.Z
                    };
                })
                .OrderBy(p => p.Date)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical prices for {Symbol} from 24hmoney", symbol);
            return null;
        }
    }

    /// <summary>
    /// <c>type=3</c> là lựa chọn duy nhất trả lợi suất theo phiên: đo ngày 2026-08-11 cho 65 điểm
    /// phủ 90 ngày, giãn cách ~1,4 ngày. <c>type=4</c> (nhãn "3 months") cho thanh 3 ngày, <c>5</c>
    /// cho thanh tuần, <c>6</c> cho thanh tháng. Không lọc theo from/to — lọc là cắt mất chính
    /// những quan sát cần cho ước lượng.
    /// </summary>
    public async Task<List<StockPriceData>> GetDailyHistoryAsync(
        string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();

        var cacheKey = $"daily-history:{symbol}";
        if (_cache.TryGetValue(cacheKey, out List<StockPriceData>? cached))
            return cached!;

        // KHÔNG bọc try/catch nuốt lỗi ở đây. Mười hàm fetch của provider số liệu doanh nghiệp đều
        // catch → log → trả rỗng, và chính cơ chế đó khiến một lệch hợp đồng sống nhiều tháng mà
        // trông y hệt "mã này không có dữ liệu" (PR #158). Ở đây hậu quả cụ thể hơn: panel sẽ nói
        // "Chưa đủ lịch sử giá cho FPT" — một câu SAI SỰ THẬT, vì FPT có thừa lịch sử, chỉ là ta
        // không lấy được. Người gọi duy nhất (VolatilityBudgetService.BackfillAsync) bắt ngoại lệ
        // và phân biệt được hai ca đó.
        var url = $"/v2/ios/stock/graph?symbol={symbol}&type={DailyGraphType}&{CommonParams}";
        var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyGraphData>>(url, JsonOptions, cancellationToken);

        var points = response?.Data?.Points;
        if (points == null || points.Count == 0)
        {
            // Rỗng THẬT: nguồn trả về đúng cấu trúc nhưng không có điểm nào. Khác hẳn lỗi mạng
            // hay lệch hợp đồng ở trên — ca này mã đó đúng là chưa có lịch sử.
            _logger.LogWarning("No daily history returned for {Symbol}", symbol);
            return new List<StockPriceData>();
        }

        var results = points
            .Select(point =>
            {
                var scaledPrice = ScalePrice(point.Y);
                return new StockPriceData
                {
                    Symbol = symbol,
                    Date = DateTimeOffset.FromUnixTimeSeconds(point.X).UtcDateTime.Date,
                    Open = scaledPrice,
                    High = scaledPrice,
                    Low = scaledPrice,
                    Close = scaledPrice,
                    Volume = point.Z
                };
            })
            .OrderBy(r => r.Date)
            .ToList();

        _cache.Set(cacheKey, results, TimeSpan.FromHours(6));
        return results;
    }

    public async Task<Dictionary<string, StockPriceData>> GetBatchPricesAsync(
        IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, StockPriceData>();
        var tasks = symbols.Select(async symbol =>
        {
            var price = await GetCurrentPriceAsync(symbol, cancellationToken);
            return (symbol: symbol.ToUpper().Trim(), price);
        });

        var completed = await Task.WhenAll(tasks);
        foreach (var (symbol, price) in completed)
        {
            if (price != null)
                result[symbol] = price;
        }

        return result;
    }

    public async Task<MarketIndexData?> GetIndexDataAsync(string indexSymbol, CancellationToken cancellationToken = default)
    {
        indexSymbol = indexSymbol.ToUpper().Trim();

        // Map input symbol → floor_code for /v1/ios/indices/detail
        var floorCode = indexSymbol switch
        {
            "VNINDEX" or "VN-INDEX" => "10",
            "VN30" or "VN30-INDEX" => "11",
            "HNX" or "HNX-INDEX" => "02",
            "UPCOM" or "UPCOM-INDEX" => "03",
            _ => null
        };

        if (floorCode == null)
        {
            _logger.LogWarning("Unknown index symbol: {IndexSymbol}", indexSymbol);
            return null;
        }

        var cacheKey = $"index_detail:{floorCode}";
        if (_cache.TryGetValue(cacheKey, out MarketIndexData? cached))
            return cached;

        try
        {
            var url = $"/v1/ios/indices/detail?floor_code={floorCode}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyIndexDetailData>>(url, JsonOptions, cancellationToken);

            var detail = response?.Data?.ShareDetail;
            if (detail == null)
            {
                _logger.LogWarning("No index detail returned for floor_code={FloorCode}", floorCode);
                return null;
            }

            // Index values are already in correct units (points) — NO scaling
            var result = new MarketIndexData
            {
                IndexSymbol = detail.Symbol,
                Date = DateTime.UtcNow.Date,
                Close = detail.MarketIndex,
                PriorClose = detail.PriorMarketIndex,
                High = detail.HighestIndex,
                Low = detail.LowestIndex,
                Average = detail.AvgIndex,
                Change = detail.Change,
                ChangePercent = detail.ChangePercent,
                Volume = (long)detail.AccumulatedVol,
                Value = detail.AccumulatedVal,
                Advance = detail.Advance,
                Decline = detail.Decline,
                NoChange = detail.NoChange,
                Ceiling = detail.Ceiling,
                Floor = detail.Floor,
                ForeignBuyValue = detail.ForeignTodayBuyValue,
                ForeignSellValue = detail.ForeignTodaySellValue,
                ForeignWeekBuyValue = detail.ForeignWeekBuyValue,
                ForeignWeekSellValue = detail.ForeignWeekSellValue,
                ForeignMonthBuyValue = detail.ForeignMonthBuyValue,
                ForeignMonthSellValue = detail.ForeignMonthSellValue
            };

            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.IndexCacheTtlSeconds));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch index detail for {IndexSymbol} (floor_code={FloorCode}) from 24hmoney", indexSymbol, floorCode);
            return null;
        }
    }

    // =============================================
    // IStockInfoProvider implementation
    // =============================================

    public async Task<StockDetailInfo?> GetStockDetailAsync(string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();
        var detail = await GetRawShareDetailAsync(symbol, cancellationToken);
        if (detail == null)
            return null;

        var price = detail.MatchPrice > 0 ? detail.MatchPrice : detail.Price;

        return new StockDetailInfo
        {
            Symbol = detail.Symbol ?? symbol,
            CompanyName = detail.CompanyName ?? "",
            CompanyNameEng = detail.CompanyNameEng ?? "",
            ShortName = detail.ShortName ?? "",
            Exchange = detail.StockExchange ?? "",
            FloorCode = detail.FloorCode ?? "",
            Price = ScalePrice(price),
            Change = ScalePrice(detail.Change),
            ChangePercent = detail.ChangePercent,
            ReferencePrice = ScalePrice(detail.BasicPrice),
            OpenPrice = ScalePrice(detail.OpenPrice),
            ClosePrice = ScalePrice(detail.ClosePrice),
            HighPrice = ScalePrice(detail.HighestPrice),
            LowPrice = ScalePrice(detail.LowestPrice),
            AveragePrice = ScalePrice(detail.AveragePrice),
            CeilingPrice = ScalePrice(detail.CeilingPrice),
            FloorPrice = ScalePrice(detail.FloorPrice),
            Volume = detail.AccumulatedVol,
            Value = detail.AccumulatedVal,
            ForeignBuyVolume = detail.BuyForeignQtty,
            ForeignSellVolume = detail.SellForeignQtty,
            ForeignRoom = detail.ForeignCurrentRoom,
            LogoUrl = detail.LogoUrl,
            Bids = new List<OrderBookLevel>
            {
                new() { Price = ScalePrice(detail.BidPrice01), Volume = detail.BidQtty01 },
                new() { Price = ScalePrice(detail.BidPrice02), Volume = detail.BidQtty02 },
                new() { Price = ScalePrice(detail.BidPrice03), Volume = detail.BidQtty03 }
            },
            Asks = new List<OrderBookLevel>
            {
                new() { Price = ScalePrice(detail.OfferPrice01), Volume = detail.OfferQtty01 },
                new() { Price = ScalePrice(detail.OfferPrice02), Volume = detail.OfferQtty02 },
                new() { Price = ScalePrice(detail.OfferPrice03), Volume = detail.OfferQtty03 }
            }
        };
    }

    public async Task<List<MarketIndexInfo>> GetMarketOverviewAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = "market_indices";

        if (_cache.TryGetValue(cacheKey, out List<MarketIndexInfo>? cached))
            return cached!;

        try
        {
            var url = $"/v1/ios/indices?{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyIndicesData>>(url, JsonOptions, cancellationToken);

            var indicesData = response?.Data;
            if (indicesData?.Floor == null)
                return new List<MarketIndexInfo>();

            // Index values are already in correct units — NO scaling
            var result = indicesData.Floor.Select(idx => new MarketIndexInfo
            {
                Symbol = idx.Symbol,
                Price = idx.Price,
                Change = idx.Change,
                ChangePercent = idx.ChangePercent,
                TotalVolume = idx.TotalShareTraded,
                TotalValue = idx.TotalValueTraded,
                TradingStatus = idx.TradingStatus,
                ForeignBuyValue = idx.ForeignTodayBuyValue,
                ForeignSellValue = idx.ForeignTodaySellValue
            }).ToList();

            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.IndexCacheTtlSeconds));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market overview from 24hmoney");
            return new List<MarketIndexInfo>();
        }
    }

    public async Task<List<StockSearchResult>> SearchStocksAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var companies = await GetCompanyListCachedAsync(cancellationToken);
        if (companies == null || companies.Count == 0)
            return new List<StockSearchResult>();

        keyword = keyword.ToUpper().Trim();

        return companies
            .Where(c =>
                c.Symbol.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (c.CompanyName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.CompanyNameEng?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.ShortName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.ExtraName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(20)
            .Select(c => new StockSearchResult
            {
                Symbol = c.Symbol,
                CompanyName = c.CompanyName ?? "",
                ShortName = c.ShortName,
                Exchange = c.Floor ?? "",
                LogoUrl = c.LogoUrl
            })
            .ToList();
    }

    public async Task<List<TopStockInfo>> GetTopFluctuationAsync(string floor = "10", CancellationToken cancellationToken = default)
    {
        var cacheKey = $"top_fluctuation:{floor}";

        if (_cache.TryGetValue(cacheKey, out List<TopStockInfo>? cached))
            return cached!;

        try
        {
            var url = $"/v1/ios/stock-recommend/top-fluctuation?floor={floor}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<List<HmoneyTopFluctuationItem>>>(url, JsonOptions, cancellationToken);

            if (response?.Data == null)
                return new List<TopStockInfo>();

            var result = response.Data.Select(item => new TopStockInfo
            {
                Symbol = item.Symbol,
                CompanyName = item.CompanyName,
                ShortName = item.ShortName,
                Price = ScalePrice(item.Price),
                Change = ScalePrice(item.Change),
                ChangePercent = item.ChangePercent,
                Volume = item.AccumulatedVol,
                CeilingPrice = ScalePrice(item.CeilingPrice),
                FloorPrice = ScalePrice(item.FloorPrice),
                ReferencePrice = ScalePrice(item.BasicPrice)
            }).ToList();

            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.PriceCacheTtlSeconds));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch top fluctuation from 24hmoney for floor {Floor}", floor);
            return new List<TopStockInfo>();
        }
    }

    public async Task<TradingHistorySummaryInfo?> GetTradingHistorySummaryAsync(string symbol, CancellationToken cancellationToken = default)
    {
        symbol = symbol.ToUpper().Trim();
        var cacheKey = $"trading_summary:{symbol}";

        if (_cache.TryGetValue(cacheKey, out TradingHistorySummaryInfo? cached))
            return cached;

        try
        {
            var url = $"/v1/ios/stock/trading-history-summary?symbol={symbol}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyTradingHistorySummary>>(url, JsonOptions, cancellationToken);

            if (response?.Data == null)
                return null;

            var result = new TradingHistorySummaryInfo
            {
                Symbol = symbol,
                ChangeDay = response.Data.ChangeDay,
                ChangeWeek = response.Data.ChangeWeek,
                ChangeMonth = response.Data.ChangeMonth,
                Change3Month = response.Data.Change3Month,
                Change6Month = response.Data.Change6Month
            };

            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.PriceCacheTtlSeconds));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch trading history summary for {Symbol} from 24hmoney", symbol);
            return null;
        }
    }

    // =============================================
    // Private helpers
    // =============================================

    private async Task<HmoneyShareDetail?> GetRawShareDetailAsync(string symbol, CancellationToken cancellationToken)
    {
        symbol = symbol.ToUpper().Trim();
        var cacheKey = $"raw_share:{symbol}";

        if (_cache.TryGetValue(cacheKey, out HmoneyShareDetail? cached))
            return cached;

        try
        {
            var url = $"/v1/ios/stock/detail?symbol={symbol}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyStockDetailData>>(url, JsonOptions, cancellationToken);
            var detail = response?.Data?.ShareDetail;
            if (detail != null)
                _cache.Set(cacheKey, detail, TimeSpan.FromSeconds(_options.PriceCacheTtlSeconds));
            return detail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stock detail for {Symbol} from 24hmoney", symbol);
            return null;
        }
    }

    private async Task<List<HmoneyCompanyItem>?> GetCompanyListCachedAsync(CancellationToken cancellationToken)
    {
        var cacheKey = "company_list";

        if (_cache.TryGetValue(cacheKey, out List<HmoneyCompanyItem>? cached))
            return cached;

        try
        {
            var url = $"/v1/ios/company/all?time_updated=0&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<List<HmoneyCompanyItem>>>(url, JsonOptions, cancellationToken);

            if (response?.Data != null)
            {
                _cache.Set(cacheKey, response.Data, TimeSpan.FromMinutes(_options.CompanyListCacheTtlMinutes));
                _logger.LogInformation("Cached {Count} companies from 24hmoney", response.Data.Count);
                return response.Data;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch company list from 24hmoney");
            return null;
        }
    }
}
