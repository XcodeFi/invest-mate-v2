using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Screener;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Screener;

public class ScreenerOptions
{
    /// <summary>Host JSON công khai của HOSE (không phải bí mật).</summary>
    public string HsxBaseUrl { get; set; } = "https://api.hsx.vn";

    /// <summary>Dữ liệu còn "tươi" trong chừng này giờ; quá hạn thì làm mới (một lần tại một thời điểm).</summary>
    public int FreshHours { get; set; } = 6;

    /// <summary>Khi làm mới lỗi, bản tốt gần nhất chỉ được phục vụ (đánh dấu cũ) tối đa chừng này giờ.</summary>
    public int MaxStaleHours { get; set; } = 48;

    /// <summary>Sau một lần làm mới lỗi, không gọi lại nguồn trong chừng này phút (tránh dồn dập khi nguồn đang chặn).</summary>
    public int FailureBackoffMinutes { get; set; } = 5;

    /// <summary>Số dòng của lần lấy mới phải nằm trong ±chừng này so với lần tốt gần nhất.</summary>
    public double RowCountTolerance { get; set; } = 0.2;

    /// <summary>Tỉ lệ dòng không dùng được (thiếu giá, vốn hoá vô lý...) tối đa trước khi coi cả lần lấy là hỏng.</summary>
    public double MaxInvalidFraction { get; set; } = 0.10;

    /// <summary>Số dòng tối thiểu cho lần lấy đầu tiên (chưa có bản tốt để so). Toàn thị trường hiện ≈ 1.800 mã.</summary>
    public int MinRows { get; set; } = 300;
}

/// <summary>Trạng thái dùng chung (singleton): bản tốt gần nhất của kho dữ liệu và của danh sách HOSE, cùng khoá làm mới.</summary>
public sealed class ScreenerStateStore
{
    internal sealed record UniverseEntry(IReadOnlyList<ScreenerRow> Rows, DateTimeOffset AsOf);
    internal sealed record HoseEntry(HoseLists Lists, DateTimeOffset AsOf);

    internal UniverseEntry? Universe;
    internal DateTimeOffset? UniverseFailedAt;
    internal string? UniverseFailure;
    internal readonly SemaphoreSlim UniverseLock = new(1, 1);

    internal HoseEntry? Hose;
    internal DateTimeOffset? HoseFailedAt;
    internal readonly SemaphoreSlim HoseLock = new(1, 1);
}

/// <summary>
/// Kho dữ liệu sàng lọc có đệm. Lấy MỘT LẦN toàn thị trường rồi áp ngưỡng trong bộ nhớ (ngưỡng không bao giờ nằm trong khoá lấy).
/// Làm mới là tất cả-hoặc-không: lỗi bất kỳ hay không đạt kiểm tra chất lượng thì giữ bản tốt gần nhất (đánh dấu cũ, tối đa 48 giờ).
/// Danh sách HOSE có bộ đệm và hạn riêng; không có danh sách thì trả null (đóng, không coi là sạch).
/// </summary>
public class MarketScreenerService : IMarketScreenerService
{
    private readonly IScreenerDataSource _source;
    private readonly ScreenerStateStore _store;
    private readonly TimeProvider _time;
    private readonly ScreenerOptions _options;
    private readonly ILogger<MarketScreenerService> _logger;

    public MarketScreenerService(IScreenerDataSource source, ScreenerStateStore store, TimeProvider time,
        IOptions<ScreenerOptions> options, ILogger<MarketScreenerService> logger)
    {
        _source = source;
        _store = store;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScreenerSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var universeTask = EnsureUniverseAsync(ct);
        var hoseTask = EnsureHoseAsync(ct);
        await Task.WhenAll(universeTask, hoseTask);

        var now = _time.GetUtcNow();
        var universe = universeTask.Result;
        var hose = hoseTask.Result;
        var notes = new List<string>();

        if (universe == null)
        {
            var reason = _store.Universe != null ? "Dữ liệu sàng lọc quá cũ" : "Không lấy được dữ liệu sàng lọc từ nguồn";
            return new ScreenerSnapshot { UnavailableReason = reason, Notes = notes };
        }

        var stale = now - universe.AsOf >= TimeSpan.FromHours(_options.FreshHours);
        if (stale)
            notes.Add(_store.UniverseFailure ?? "Lần làm mới dữ liệu gần nhất bị lỗi; đang dùng dữ liệu cũ.");

        var hoseStale = hose != null && now - hose.AsOf >= TimeSpan.FromHours(_options.FreshHours);
        return new ScreenerSnapshot
        {
            Rows = universe.Rows,
            AsOf = universe.AsOf,
            Stale = stale,
            Hose = hose?.Lists,
            HoseAsOf = hose?.AsOf,
            HoseStale = hoseStale,
            Notes = notes
        };
    }

    private async Task<ScreenerStateStore.UniverseEntry?> EnsureUniverseAsync(CancellationToken ct)
    {
        if (IsFresh(_store.Universe?.AsOf)) return _store.Universe;

        await _store.UniverseLock.WaitAsync(ct);
        try
        {
            if (!IsFresh(_store.Universe?.AsOf) && !InBackoff(_store.UniverseFailedAt))
            {
                try
                {
                    var fetch = await _source.FetchUniverseAsync(ct);
                    var problem = Validate(fetch, _store.Universe?.Rows.Count);
                    if (problem != null)
                    {
                        Fail(problem);
                    }
                    else
                    {
                        _store.Universe = new ScreenerStateStore.UniverseEntry(fetch.Rows, _time.GetUtcNow());
                        _store.UniverseFailedAt = null;
                        _store.UniverseFailure = null;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is HttpRequestException or FormatException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
                {
                    _logger.LogWarning(ex, "Screener: làm mới kho dữ liệu lỗi, giữ bản tốt gần nhất");
                    Fail("Lần làm mới dữ liệu gần nhất bị lỗi nguồn; đang dùng dữ liệu cũ.");
                }
            }
        }
        finally { _store.UniverseLock.Release(); }

        var entry = _store.Universe;
        return entry != null && _time.GetUtcNow() - entry.AsOf <= TimeSpan.FromHours(_options.MaxStaleHours) ? entry : null;

        void Fail(string message)
        {
            _store.UniverseFailedAt = _time.GetUtcNow();
            _store.UniverseFailure = message;
        }
    }

    private async Task<ScreenerStateStore.HoseEntry?> EnsureHoseAsync(CancellationToken ct)
    {
        if (!IsFresh(_store.Hose?.AsOf))
        {
            await _store.HoseLock.WaitAsync(ct);
            try
            {
                if (!IsFresh(_store.Hose?.AsOf) && !InBackoff(_store.HoseFailedAt))
                {
                    try
                    {
                        var lists = await _source.FetchHoseListsAsync(ct);
                        _store.Hose = new ScreenerStateStore.HoseEntry(lists, _time.GetUtcNow());
                        _store.HoseFailedAt = null;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (ex is HttpRequestException or FormatException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
                    {
                        _logger.LogWarning(ex, "Screener: làm mới danh sách HOSE lỗi");
                        _store.HoseFailedAt = _time.GetUtcNow();
                    }
                }
            }
            finally { _store.HoseLock.Release(); }
        }

        var entry = _store.Hose;
        return entry != null && _time.GetUtcNow() - entry.AsOf <= TimeSpan.FromHours(_options.MaxStaleHours) ? entry : null;
    }

    /// <summary>Cổng chất lượng của một lần lấy mới; trả lý do nếu không đạt (rồi giữ bản tốt cũ).</summary>
    private string? Validate(ScreenerUniverseFetch fetch, int? lastGoodCount)
    {
        const string prefix = "Lần làm mới dữ liệu gần nhất không đạt kiểm tra chất lượng";
        if (fetch.RsOutOfRangeCount > 0)
            return $"{prefix} (xếp hạng sức mạnh ngoài khoảng 1–99); đang dùng dữ liệu cũ.";
        if (fetch.Rows.Count == 0 || fetch.RawCount == 0)
            return $"{prefix} (không có dòng nào); đang dùng dữ liệu cũ.";
        if ((double)fetch.InvalidCount / fetch.RawCount > _options.MaxInvalidFraction)
            return $"{prefix} (quá nhiều dòng thiếu giá hoặc vốn hoá); đang dùng dữ liệu cũ.";
        if (lastGoodCount is { } last)
        {
            if (Math.Abs(fetch.Rows.Count - last) > last * _options.RowCountTolerance)
                return $"{prefix} (số mã lệch hơn {(int)(_options.RowCountTolerance * 100)}% so với lần trước); đang dùng dữ liệu cũ.";
        }
        else if (fetch.Rows.Count < _options.MinRows)
            return $"{prefix} (số mã quá ít); chưa có dữ liệu để dùng.";
        return null;
    }

    private bool IsFresh(DateTimeOffset? asOf)
        => asOf is { } t && _time.GetUtcNow() - t < TimeSpan.FromHours(_options.FreshHours);

    private bool InBackoff(DateTimeOffset? failedAt)
        => failedAt is { } t && _time.GetUtcNow() - t < TimeSpan.FromMinutes(_options.FailureBackoffMinutes);
}
