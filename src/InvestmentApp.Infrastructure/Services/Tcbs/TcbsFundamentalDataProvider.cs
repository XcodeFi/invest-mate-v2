using System.Net.Http.Json;
using System.Text.Json;
using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services.Tcbs;

/// <summary>
/// Fundamental data provider using TCBS public API.
/// Endpoint: https://apipubaws.tcbs.com.vn/tcanalysis/v1/ticker/{symbol}/overview
/// </summary>
public class TcbsFundamentalDataProvider : IFundamentalDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TcbsFundamentalDataProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TcbsFundamentalDataProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<TcbsFundamentalDataProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<StockFundamentalData?> GetFundamentalsAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.ToUpper().Trim();
        var cacheKey = $"tcbs_fundamental:{symbol}";

        if (_cache.TryGetValue(cacheKey, out StockFundamentalData? cached))
            return cached;

        try
        {
            var url = $"/tcanalysis/v1/ticker/{symbol}/overview";
            var response = await _httpClient.GetFromJsonAsync<TcbsOverviewResponse>(url, JsonOptions, ct);

            if (response == null)
            {
                _logger.LogWarning("No fundamental data returned for {Symbol} from TCBS", symbol);
                return null;
            }

            var result = new StockFundamentalData
            {
                Symbol = symbol,
                Industry = response.Industry,
                MarketCap = response.MarketCap,
                PE = response.PE,
                PB = response.PB,
                EPS = response.EPS,
                ROE = response.ROE != null ? response.ROE * 100 : null,   // Convert ratio → %
                ROA = response.ROA != null ? response.ROA * 100 : null,
                DebtToEquity = response.DebtOnEquity,
                DividendYield = response.DividendYield != null ? response.DividendYield * 100 : null,
                Revenue = response.Revenue,
                RevenueGrowth = response.RevenueGrowth != null ? response.RevenueGrowth * 100 : null,
                NetProfit = response.NetProfit,
                NetProfitGrowth = response.NetProfitGrowth != null ? response.NetProfitGrowth * 100 : null,
                ForeignPercent = response.ForeignPercent != null ? response.ForeignPercent * 100 : null,
                OutstandingShares = response.OutstandingShare
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch fundamental data for {Symbol} from TCBS", symbol);
            return null;
        }
    }
}
