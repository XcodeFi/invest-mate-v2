using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

public class BankRateProviderOptions
{
    public string PageUrl { get; set; } = "https://24hmoney.vn/lai-suat-gui-ngan-hang";
    public int TimeoutSeconds { get; set; } = 30;
    public int FreshCacheHours { get; set; } = 6;
    public int StaleCacheHours { get; set; } = 24;
    public string UserAgent { get; set; } = "invest-mate-bank-rate-crawler/1.0";
}

/// <summary>
/// Crawler lãi suất tiết kiệm từ 24hmoney.vn/lai-suat-gui-ngan-hang. SSR HTML — parse với AngleSharp.
/// Trang có 2 bảng: <c>.bank-rate-offline</c> (gửi quầy) và <c>.bank-rate-online</c> (gửi online).
/// Ta **ưu tiên online** vì lãi suất online cao hơn quầy 0.2-0.8% (user thực tế gửi qua app).
/// Mỗi bảng 5 cột kỳ hạn: 1T / 3T / 6T / 9T / 12T. Cell "-" = không công bố → skip.
/// Cache 2-tier: fresh 6h + stale 24h. Trang refresh 1 lần/ngày nên TTL 6h là safe.
/// </summary>
public class HmoneyBankRateProvider : IBankRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HmoneyBankRateProvider> _logger;
    private readonly BankRateProviderOptions _options;

    private const string CacheKeyFresh = "hmoney_bank_rates";
    private const string CacheKeyStale = "hmoney_bank_rates_stale";

    // Column index (0-based, sau cột bank name) → số tháng kỳ hạn.
    // Thứ tự trong HTML: Ngân hàng | 01 tháng | 03 tháng | 06 tháng | 09 tháng | 12 tháng
    private static readonly int[] TermMonthsByColumn = { 1, 3, 6, 9, 12 };

    public HmoneyBankRateProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<HmoneyBankRateProvider> logger,
        IOptions<BankRateProviderOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<BankRateSnapshot> GetTopRatesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<BankRateSnapshot>(CacheKeyFresh, out var cached) && cached is not null)
            return cached;

        try
        {
            var html = await FetchHtmlAsync(cancellationToken);
            var snapshot = await ParseHtmlAsync(html, DateTime.UtcNow, cancellationToken);

            _cache.Set(CacheKeyFresh, snapshot, TimeSpan.FromHours(_options.FreshCacheHours));
            _cache.Set(CacheKeyStale, snapshot, TimeSpan.FromHours(_options.StaleCacheHours));
            _logger.LogInformation("Fetched bank rate snapshot from 24hmoney ({TermCount} terms)", snapshot.TopByTerm.Count);
            return snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_cache.TryGetValue<BankRateSnapshot>(CacheKeyStale, out var stale) && stale is not null)
            {
                _logger.LogWarning(ex, "Fetch bank rates failed — returning stale cache");
                return stale;
            }

            _logger.LogError(ex, "Fetch bank rates failed and no stale cache available");
            throw;
        }
    }

    private async Task<string> FetchHtmlAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await _httpClient.GetAsync(_options.PageUrl, linkedCts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(linkedCts.Token);
    }

    /// <summary>
    /// Pure HTML parser — testable without HTTP. Expects `.bank-rate-online` section (fallback offline if missing)
    /// với table → tbody → tr rows. Mỗi row: 1 `a.name` (bank name) + 5 `p.bank-interest-rate` (1T/3T/6T/9T/12T).
    /// Cells "-" = không công bố → skip term đó cho bank này.
    /// Public để test qua fixture không cần HTTP roundtrip.
    /// </summary>
    public static async Task<BankRateSnapshot> ParseHtmlAsync(string html, DateTime crawlTime, CancellationToken cancellationToken)
    {
        var config = AngleSharp.Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        // Ưu tiên online table (plan D3: "gửi online" cao hơn quầy)
        var onlineTable = document.QuerySelector("div.bank-rate-online table tbody");
        var offlineTable = document.QuerySelector("div.bank-rate-offline table tbody");
        var tbody = onlineTable ?? offlineTable;

        if (tbody is null)
        {
            return new BankRateSnapshot(
                new Dictionary<int, BankRateEntry>(),
                SourceTimestamp: null,
                FetchedAt: crawlTime);
        }

        // Tìm top rate per term
        var topByTerm = new Dictionary<int, BankRateEntry>();

        foreach (var row in tbody.QuerySelectorAll("tr"))
        {
            var bankName = row.QuerySelector("a.name")?.TextContent?.Trim();
            if (string.IsNullOrEmpty(bankName)) continue;

            var rateCells = row.QuerySelectorAll("p.bank-interest-rate").ToList();
            for (var i = 0; i < TermMonthsByColumn.Length && i < rateCells.Count; i++)
            {
                if (!TryParseRate(rateCells[i].TextContent, out var ratePercent))
                    continue; // "-" hoặc invalid → skip

                var term = TermMonthsByColumn[i];
                if (!topByTerm.TryGetValue(term, out var existing) || ratePercent > existing.RatePercent)
                {
                    topByTerm[term] = new BankRateEntry(term, ratePercent, bankName);
                }
            }
        }

        var sourceTimestamp = ExtractSourceTimestamp(document);

        return new BankRateSnapshot(topByTerm, sourceTimestamp, crawlTime);
    }

    /// <summary>
    /// Extract "23:59:59 25/03/2026" từ dòng "Cập nhật lúc: 23:59:59 25/03/2026".
    /// Null nếu không parse được (không block crawl).
    /// </summary>
    private static DateTime? ExtractSourceTimestamp(IDocument document)
    {
        var updateTimeElement = document.QuerySelector("div.bank-rate-online .bank-rate-update-time")
                              ?? document.QuerySelector(".bank-rate-update-time");
        var text = updateTimeElement?.TextContent;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text, @"(\d{2}:\d{2}:\d{2})\s+(\d{2}/\d{2}/\d{4})");
        if (!match.Success) return null;

        if (DateTime.TryParseExact(
                $"{match.Groups[2].Value} {match.Groups[1].Value}",
                "dd/MM/yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static bool TryParseRate(string text, out decimal value)
    {
        var cleaned = text.Trim();
        if (cleaned == "-" || string.IsNullOrEmpty(cleaned))
        {
            value = 0m;
            return false;
        }
        return decimal.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
