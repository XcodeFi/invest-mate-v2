using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>
/// Chọn mã VN30 cần lấy trước: mã chưa có chuỗi đi trước (theo thứ tự rổ), rồi mã có chuỗi cũ nhất. Bỏ mã mà nguồn coi là còn tươi
/// (đúng luật của nguồn, không tốn lượt cho thứ bộ nhớ đệm sẽ trả lời). Mọi giới hạn gọi nằm ở phía nguồn (<see cref="IAdjustedSeriesWarmer"/>);
/// gặp trục trặc (nguồn nghỉ, hết ngân sách, lỗi) thì dừng lượt này.
/// </summary>
public class SignalSeriesWarmup : ISignalSeriesWarmup
{
    private readonly IAdjustedPriceSeriesStore _store;
    private readonly IAdjustedSeriesWarmer _warmer;
    private readonly SignalWarmupOptions _options;
    private readonly ILogger<SignalSeriesWarmup> _logger;

    public SignalSeriesWarmup(
        IAdjustedPriceSeriesStore store, IAdjustedSeriesWarmer warmer, IOptions<SignalWarmupOptions> options, ILogger<SignalSeriesWarmup> logger)
    {
        _store = store;
        _warmer = warmer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SignalWarmupOutcome> WarmNextAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.SymbolsPerRun <= 0)
            return new SignalWarmupOutcome(0, "disabled");

        var fetchedAt = await _store.GetFetchedAtAsync(Vn30Basket.Symbols.ToList(), cancellationToken);

        var candidates = Vn30Basket.Symbols
            .Select((symbol, index) => (Symbol: symbol, Index: index, At: fetchedAt.TryGetValue(symbol, out var at) ? (DateTime?)at : null))
            .Where(c => c.At == null || !_warmer.IsFresh(c.At.Value))
            .OrderBy(c => c.At.HasValue)              // chưa có chuỗi trước
            .ThenBy(c => c.At ?? DateTime.MinValue)   // rồi cũ nhất
            .ThenBy(c => c.Index)
            .Select(c => c.Symbol)
            .ToList();

        var fetched = 0;
        foreach (var symbol in candidates)
        {
            if (fetched >= _options.SymbolsPerRun) break;

            var result = await _warmer.WarmAsync(symbol, cancellationToken);
            switch (result)
            {
                case WarmFetchResult.Fetched:
                    fetched++;
                    break;
                case WarmFetchResult.Skipped:
                    continue;   // không tốn lượt: thử mã kế
                default:
                    _logger.LogInformation("Signal warm-up stopped at {Symbol}: {Result}", symbol, result);
                    return new SignalWarmupOutcome(fetched, result.ToString());
            }
        }

        return new SignalWarmupOutcome(fetched, candidates.Count == 0 ? "all-fresh" : "ok");
    }
}
