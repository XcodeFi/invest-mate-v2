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

    // 24hmoney returns stock prices in units of 1,000 VND
    private const decimal PriceScale = 1000m;

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
        IOptions<MarketDataProviderOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
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

        var days = (to - from).TotalDays;
        int graphType = days switch
        {
            <= 1 => 1,   // Intraday
            <= 14 => 7,  // 2 weeks
            <= 35 => 3,  // 1 month
            <= 100 => 4, // 3 months
            <= 400 => 5, // 1 year
            _ => 6       // 5 years
        };

        var cacheKey = $"history:{symbol}:{graphType}";
        if (_cache.TryGetValue(cacheKey, out List<StockPriceData>? cached))
            return cached!;

        var results = new List<StockPriceData>();

        try
        {
            var url = $"/v2/ios/stock/graph?symbol={symbol}&type={graphType}&{CommonParams}";
            var response = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyGraphData>>(url, JsonOptions, cancellationToken);

            var graphData = response?.Data;
            if (graphData?.Points == null || graphData.Points.Count == 0)
            {
                _logger.LogWarning("No historical graph data returned for {Symbol}", symbol);
                return results;
            }

            foreach (var point in graphData.Points)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(point.X).UtcDateTime.Date;
                if (date < from.Date || date > to.Date)
                    continue;

                var scaledPrice = ScalePrice(point.Y);
                results.Add(new StockPriceData
                {
                    Symbol = symbol,
                    Date = date,
                    Open = scaledPrice,
                    High = scaledPrice,
                    Low = scaledPrice,
                    Close = scaledPrice,
                    Volume = point.Z
                });
            }

            // Historical data changes less frequently — cache 60s
            _cache.Set(cacheKey, results, TimeSpan.FromSeconds(60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical prices for {Symbol} from 24hmoney", symbol);
        }

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
