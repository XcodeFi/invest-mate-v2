using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

public class GoldPriceProviderOptions
{
    public string PageUrl { get; set; } = "https://24hmoney.vn/gia-vang";
    public int TimeoutSeconds { get; set; } = 30;
    public int CacheTtlMinutes { get; set; } = 5;
}

/// <summary>
/// Crawler giá vàng từ 24hmoney.vn/gia-vang. Data render SSR trong HTML table.gold-table — không có JSON API.
/// Parse bằng AngleSharp, filter chỉ Miếng + Nhẫn (skip Nữ trang + Trang sức).
/// Giá trị là full VND (mặc dù UI label nói "triệu VNĐ/lượng") — không nhân 1000.
/// Cache 5 phút (default) — vàng update chậm hơn CP nhiều.
/// </summary>
public class HmoneyGoldPriceProvider : IGoldPriceProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HmoneyGoldPriceProvider> _logger;
    private readonly GoldPriceProviderOptions _options;

    private const string CacheKeyFresh = "hmoney_gold_prices";
    private const string CacheKeyStale = "hmoney_gold_prices_stale";
    // Stale cache TTL: vàng update chậm → 6h là OK cho fallback khi 24hmoney downtime
    private static readonly TimeSpan StaleCacheTtl = TimeSpan.FromHours(6);

    public HmoneyGoldPriceProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<HmoneyGoldPriceProvider> logger,
        IOptions<GoldPriceProviderOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<GoldPriceDto>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<GoldPriceDto>>(CacheKeyFresh, out var cached) && cached is not null)
            return cached;

        try
        {
            var html = await FetchHtmlAsync(cancellationToken);
            var prices = await ParseHtmlAsync(html, DateTime.UtcNow, cancellationToken);

            _cache.Set(CacheKeyFresh, prices, TimeSpan.FromMinutes(_options.CacheTtlMinutes));
            _cache.Set(CacheKeyStale, prices, StaleCacheTtl);
            _logger.LogInformation("Fetched {Count} gold prices from 24hmoney", prices.Count);
            return prices;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Stale fallback: vàng thay đổi chậm (5-10 lần/ngày). Serve dữ liệu cũ tốt hơn là 500 error.
            if (_cache.TryGetValue<IReadOnlyList<GoldPriceDto>>(CacheKeyStale, out var stale) && stale is not null)
            {
                _logger.LogWarning(ex, "Fetch gold prices failed — returning stale cache ({Count} items)", stale.Count);
                return stale;
            }

            _logger.LogError(ex, "Fetch gold prices failed and no stale cache available");
            throw;
        }
    }

    public async Task<GoldPriceDto?> GetPriceAsync(GoldBrand brand, GoldType type, CancellationToken cancellationToken = default)
    {
        var all = await GetPricesAsync(cancellationToken);
        // Note: với brand=Other (gộp BTMC/BTMH/Ngọc Hải/Mi Hồng...), FirstOrDefault trả entry xuất hiện đầu
        // trong HTML 24hmoney (hiện là BTMC Miếng / BTMH Nhẫn). Nếu 24hmoney đổi thứ tự row, caller sẽ
        // thấy price khác mà không có indicator. Solo user acceptable — document để sau không confuse.
        return all.FirstOrDefault(p => p.Brand == brand && p.Type == type);
    }

    private async Task<string> FetchHtmlAsync(CancellationToken cancellationToken)
    {
        // Apply timeout defensively — HttpClient.Timeout có thể không set trong DI.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await _httpClient.GetAsync(_options.PageUrl, linkedCts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(linkedCts.Token);
    }

    /// <summary>
    /// Pure HTML parser — testable without HTTP. Expects table.gold-table structure từ 24hmoney.vn/gia-vang
    /// với section dividers (SJC/DOJI/PNJ/KHÁC) và data rows có div.brand-region + 2 td.price-cell.
    /// Filter: chỉ keep "vàng miếng" + "vàng nhẫn" — skip "vàng nữ trang" + "vàng trang sức".
    /// Public to allow fixture-based unit tests without HTTP roundtrip.
    /// </summary>
    public static async Task<IReadOnlyList<GoldPriceDto>> ParseHtmlAsync(string html, DateTime crawlTime, CancellationToken cancellationToken)
    {
        var config = AngleSharp.Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        var tbody = document.QuerySelector("table.gold-table tbody");
        if (tbody is null) return Array.Empty<GoldPriceDto>();

        var result = new List<GoldPriceDto>();
        var currentBrand = GoldBrand.Other;

        foreach (var row in tbody.QuerySelectorAll("tr"))
        {
            // Section divider row: <tr class="divider-row"><td colspan="4">SJC|DOJI|PNJ|KHÁC</td></tr>
            if (row.ClassList.Contains("divider-row"))
            {
                currentBrand = MapBrand(row.TextContent);
                continue;
            }

            // Data row
            var region = row.QuerySelector("div.brand-region")?.TextContent;
            var goldType = MapType(region);
            if (goldType is null) continue; // skip nữ trang / trang sức

            var priceCells = row.QuerySelectorAll("td.price-cell div.price-today").ToList();
            if (priceCells.Count < 2)
            {
                continue; // defensive — unexpected structure
            }

            if (!TryParsePrice(priceCells[0].TextContent, out var buyPrice)) continue;
            if (!TryParsePrice(priceCells[1].TextContent, out var sellPrice)) continue;

            result.Add(new GoldPriceDto
            {
                Brand = currentBrand,
                Type = goldType.Value,
                BuyPrice = buyPrice,
                SellPrice = sellPrice,
                UpdatedAt = crawlTime,
            });
        }

        return result;
    }

    private static GoldBrand MapBrand(string dividerText) => dividerText.Trim().ToUpperInvariant() switch
    {
        "SJC" => GoldBrand.SJC,
        "DOJI" => GoldBrand.DOJI,
        "PNJ" => GoldBrand.PNJ,
        _ => GoldBrand.Other, // "KHÁC" hoặc brand mới chưa biết
    };

    private static GoldType? MapType(string? region)
    {
        var normalized = region?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "vàng miếng" => GoldType.Mieng,
            "vàng nhẫn" => GoldType.Nhan,
            _ => null, // nữ trang, trang sức, null → skip
        };
    }

    // Assumption: 24hmoney render giá dạng integer full VND với "," hoặc "." làm thousand separator
    // (VD: "167,200,000" hoặc "167.200.000"). Nếu sau này họ đổi sang abbreviated display ("169.5" triệu)
    // thì parser này sẽ sai 1000× — cần monitor fixture test khi upgrade.
    private static bool TryParsePrice(string text, out decimal value)
    {
        var cleaned = text.Trim().Replace(",", "").Replace(".", "");
        return decimal.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
