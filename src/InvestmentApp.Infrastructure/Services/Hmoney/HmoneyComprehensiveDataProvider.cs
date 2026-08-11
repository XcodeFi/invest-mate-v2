using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Hmoney;

/// <summary>
/// Fetches comprehensive stock data from 24hmoney.vn APIs for AI analysis reports.
/// Endpoints used:
///   /v2/ios/companies/index          — P/E, P/B, ROE, ROA, EPS, MarketCap, Beta
///   /v1/ios/company/detail           — shareholders, leadership
///   /v1/ios/company/financial-report  — quarterly income statement
///   /v1/ios/company/plan             — business plan targets
///   /v1/ios/announcement/dividend-events — dividend/ĐHCĐ events
///   /v1/ios/stock-recommend/get_stock_related_bussiness — peer comparison
///   /v1/ios/stock/foreign-trading-series — foreign net buy/sell
///   /v1/ios/announcement/report-analytics — analyst reports
///   /v1/ios/indices/detail           — VN-Index snapshot
/// </summary>
public class HmoneyComprehensiveDataProvider : IComprehensiveStockDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HmoneyComprehensiveDataProvider> _logger;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const string CommonParams =
        "device_id=web&locale=vi&device_name=INVALID&device_model=INVALID" +
        "&network_carrier=INVALID&connection_type=INVALID&os=Chrome&os_version=1" +
        "&access_token=INVALID&push_token=INVALID";

    public HmoneyComprehensiveDataProvider(
        HttpClient httpClient,
        ILogger<HmoneyComprehensiveDataProvider> logger,
        IOptions<MarketDataProviderOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = options.Value.BaseUrl;
    }

    public async Task<ComprehensiveStockData?> GetComprehensiveDataAsync(
        string symbol, CancellationToken ct = default)
    {
        symbol = symbol.ToUpper().Trim();

        // Fire all API calls in parallel — individual failures should not crash the whole request
        var indicatorsTask = GetFinanceIndicatorsAsync(symbol, ct);
        var companyTask = GetCompanyDetailAsync(symbol, ct);
        var identityTask = GetStockIdentityAsync(symbol, ct);
        var incomeTask = GetIncomeStatementAsync(symbol, ct);
        var peersTask = GetPeersAsync(symbol, ct);
        var dividendTask = GetDividendEventsAsync(symbol, ct);
        var planTask = GetBusinessPlanAsync(symbol, ct);
        var reportsTask = GetAnalystReportsAsync(symbol, ct);
        var foreignTask = GetForeignTradingAsync(symbol, ct);
        var indexTask = GetMarketIndexAsync(ct);

        await Task.WhenAll(
            indicatorsTask.ContinueWith(_ => { }, ct),
            companyTask.ContinueWith(_ => { }, ct),
            identityTask.ContinueWith(_ => { }, ct),
            incomeTask.ContinueWith(_ => { }, ct),
            peersTask.ContinueWith(_ => { }, ct),
            dividendTask.ContinueWith(_ => { }, ct),
            planTask.ContinueWith(_ => { }, ct),
            reportsTask.ContinueWith(_ => { }, ct),
            foreignTask.ContinueWith(_ => { }, ct),
            indexTask.ContinueWith(_ => { }, ct)
        );

        var indicators = indicatorsTask.IsCompletedSuccessfully ? indicatorsTask.Result : null;
        var company = companyTask.IsCompletedSuccessfully ? companyTask.Result : null;
        var identity = identityTask.IsCompletedSuccessfully ? identityTask.Result : null;

        // At minimum we need indicators or company data
        if (indicators == null && company == null)
        {
            _logger.LogWarning("No data available for symbol {Symbol}", symbol);
            return null;
        }

        var result = new ComprehensiveStockData { Symbol = symbol };

        // Map company overview
        if (company != null || indicators != null)
        {
            result.Company = new CompanyOverview
            {
                CompanyName = identity?.CompanyName?.Trim(),
                ShortName = identity?.ShortName?.Trim(),
                Exchange = identity?.StockExchange,
                Industry = indicators?.GroupName,
                ListedShares = indicators?.ListedShareVol,
                OutstandingShares = indicators?.CirculationVol,
                FreeFloatRate = indicators?.FreeFloatRate.HasValue == true
                    ? indicators.FreeFloatRate.Value * 100m : null,
                MajorShareholders = company?.Ownership?
                    .Take(10)
                    .Select(s => new Application.Interfaces.Shareholder
                    {
                        Name = s.Name,
                        Quantity = ParseDecimal(s.Stock) ?? 0m,
                        Percentage = ParseDecimal(s.Value) ?? 0m
                    }).ToList() ?? new(),
                Leaders = company?.Leadership?
                    .Take(10)
                    .Select(l => new Application.Interfaces.CompanyLeader
                    {
                        Name = l.Name,
                        Position = l.Positions?.FirstOrDefault()?.Position
                    }).ToList() ?? new()
            };
        }

        // Map finance indicators
        if (indicators != null)
        {
            result.Indicators = new Application.Interfaces.FinanceIndicators
            {
                PE = indicators.PE,
                PE4Q = indicators.PE4Q,
                PB = indicators.PB,
                PB4Q = indicators.PB4Q,
                EPS = indicators.EPS,
                EPS4Q = indicators.EPS4Q,
                ROE = indicators.ROE,
                ROE4Q = indicators.ROE4Q,
                ROA = indicators.ROA,
                ROA4Q = indicators.ROA4Q,
                MarketCap = indicators.MarketCap,
                BookValue = indicators.BookValue,
                Beta = indicators.Beta,
                EvPerEbitda = indicators.EvPerEbitda,
                EvPerEbit = indicators.EvPerEbit,
                FreeFloatRate = indicators.FreeFloatRate.HasValue
                    ? indicators.FreeFloatRate.Value * 100m : null,
                Min52W = indicators.Min52W,
                Max52W = indicators.Max52W,
                IndustryGroup = indicators.GroupName,
                AuditFirmName = indicators.AuditFirmName,
                AuditIsBig4 = indicators.AuditIsBig4
            };
        }

        // Map income statements
        if (incomeTask.IsCompletedSuccessfully && incomeTask.Result != null)
            result.IncomeStatements = incomeTask.Result;

        // Map peers
        if (peersTask.IsCompletedSuccessfully && peersTask.Result != null)
            result.Peers = peersTask.Result;

        // Map dividend events
        if (dividendTask.IsCompletedSuccessfully && dividendTask.Result != null)
            result.DividendEvents = dividendTask.Result;

        // Map business plan
        if (planTask.IsCompletedSuccessfully && planTask.Result != null)
            result.BusinessPlan = planTask.Result;

        // Map analyst reports
        if (reportsTask.IsCompletedSuccessfully && reportsTask.Result != null)
            result.AnalystReports = reportsTask.Result;

        // Map foreign trading
        if (foreignTask.IsCompletedSuccessfully && foreignTask.Result != null)
            result.ForeignTrading = foreignTask.Result;

        // Map market index
        if (indexTask.IsCompletedSuccessfully && indexTask.Result != null)
            result.MarketIndex = indexTask.Result;

        return result;
    }

    // =============================================
    // Private API fetch methods
    // =============================================

    private async Task<HmoneyFinanceIndicators?> GetFinanceIndicatorsAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v2/ios/companies/index?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyFinanceIndicators>>(url, JsonOptions, ct);
            return resp?.Status == 200 ? resp.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get finance indicators for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<HmoneyCompanyDetail?> GetCompanyDetailAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/company/detail?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyCompanyDetail>>(url, JsonOptions, ct);
            return resp?.Status == 200 ? resp.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get company detail for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// `/company/detail` không còn trả tên công ty và sàn, nên lấy từ `/stock/detail` —
    /// endpoint đang dùng cho giá nên vẫn chạy.
    /// </summary>
    private async Task<HmoneyShareDetail?> GetStockIdentityAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/stock/detail?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyStockDetailData>>(url, JsonOptions, ct);
            return resp?.Status == 200 ? resp.Data?.ShareDetail : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stock identity for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<List<IncomeStatementItem>?> GetIncomeStatementAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            // view=2 = báo cáo kết quả kinh doanh, period=2 = theo quý (period=1 trả theo năm)
            var url = $"{_baseUrl}/v1/ios/company/financial-report?symbol={symbol}&view=2&period=2&expanded=false&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyFinancialReportData>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data?.Headers == null || resp.Data.Rows == null)
                return null;

            var headers = resp.Data.Headers;
            var rows = resp.Data.Rows;

            // Nguồn viết hoa không nhất quán ("LỢI NHUẬN SAU THUẾ TNDN"), nên so khớp phân biệt
            // hoa thường sẽ trả null mà không báo lỗi gì.
            var revenueRow = FindRow(rows, "Doanh thu thuần");
            var profitRow = FindRow(rows, "Lợi nhuận sau thuế");
            var grossProfitRow = FindRow(rows, "Lợi nhuận gộp");

            var items = new List<IncomeStatementItem>();
            for (int i = 0; i < headers.Count && i < 8; i++) // 8 kỳ gần nhất
            {
                items.Add(new IncomeStatementItem
                {
                    Period = FormatPeriod(headers[i]),
                    Revenue = GetValueAtIndex(revenueRow?.Values, i),
                    NetProfit = GetValueAtIndex(profitRow?.Values, i),
                    GrossProfit = GetValueAtIndex(grossProfitRow?.Values, i)
                });
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get income statement for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<List<PeerStock>?> GetPeersAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/stock-recommend/get_stock_related_bussiness?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyPeersData>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data?.All?.Data == null)
                return null;

            return resp.Data.All.Data
                .Where(p => !string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(p => new PeerStock
                {
                    Symbol = p.Symbol,
                    CompanyName = p.CompanyName,
                    Price = p.Price.HasValue ? p.Price.Value * 1000m : null, // Scale ×1000
                    PE = p.PE,
                    PB = p.PB,
                    MarketCap = p.MarketCap,
                    ChangePercent = p.ChangePercent
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get peers for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<List<DividendEvent>?> GetDividendEventsAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/announcement/dividend-events?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<List<HmoneyDividendEvent>>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data == null)
                return null;

            return resp.Data.Take(10).Select(e => new DividendEvent
            {
                EventType = e.Type,
                Description = e.Title,
                ExDate = FormatVietnamDate(e.ExRightDate),
                PayDate = FormatVietnamDate(e.PayoutDate),
                // Nguồn không còn trả trường số riêng; giá trị chỉ nằm trong câu tiêu đề.
                // Bóc số từ câu tiếng Việt mong manh hơn là để trống.
                Value = null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get dividend events for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<CompanyPlan?> GetBusinessPlanAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/company/plan?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyCompanyPlanData>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data?.Plan == null || resp.Data.Plan.Count == 0)
                return null;

            return new CompanyPlan
            {
                Year = resp.Data.Year,
                Quarter = resp.Data.Quarter,
                // Giữ nguyên nhãn nguồn trả về thay vì so khớp sang tên cố định: so khớp nhãn
                // tiếng Việt sẽ âm thầm rụng chỉ tiêu ngay khi nguồn đổi cách viết.
                Targets = resp.Data.Plan.Select(p => new CompanyPlanTarget
                {
                    Label = p.Label,
                    Planned = p.Expect,
                    Actual = p.Current,
                    PercentComplete = p.Percent
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get business plan for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<List<AnalystReport>?> GetAnalystReportsAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/announcement/report-analytics?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<List<HmoneyAnalystReport>>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data == null)
                return null;

            return resp.Data.Take(5).Select(r => new AnalystReport
            {
                Title = r.Title,
                Source = r.Source,
                PublishDate = r.PublishDate,
                Summary = r.Summary
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get analyst reports for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<ForeignTradingSummary?> GetForeignTradingAsync(
        string symbol, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/stock/foreign-trading-series?symbol={symbol}&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyForeignTradingData>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data == null)
                return null;

            return new ForeignTradingSummary
            {
                TodayBuyValue = resp.Data.TodayBuyValue,
                TodaySellValue = resp.Data.TodaySellValue,
                WeekBuyValue = resp.Data.WeekBuyValue,
                WeekSellValue = resp.Data.WeekSellValue,
                MonthBuyValue = resp.Data.MonthBuyValue,
                MonthSellValue = resp.Data.MonthSellValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get foreign trading for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<MarketIndexSnapshot?> GetMarketIndexAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{_baseUrl}/v1/ios/indices/detail?floor_code=10&{CommonParams}";
            var resp = await _httpClient.GetFromJsonAsync<HmoneyResponse<HmoneyIndexDetailData>>(url, JsonOptions, ct);
            if (resp?.Status != 200 || resp.Data?.ShareDetail == null)
                return null;

            var detail = resp.Data.ShareDetail;
            return new MarketIndexSnapshot
            {
                Value = detail.MarketIndex,
                Change = detail.Change,
                ChangePercent = detail.ChangePercent,
                Advances = detail.Advance,
                Declines = detail.Decline,
                NoChange = detail.NoChange,
                TotalVolume = detail.AccumulatedVol / 1_000_000m, // triệu CP
                TotalValue = detail.AccumulatedVal,               // tỷ VND
                ForeignBuyValue = detail.ForeignTodayBuyValue,    // tỷ VND
                ForeignSellValue = detail.ForeignTodaySellValue   // tỷ VND
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get VN-Index data");
            return null;
        }
    }

    private static decimal? GetValueAtIndex(List<decimal?>? values, int index)
    {
        if (values == null || index >= values.Count) return null;
        return values[index];
    }

    private static HmoneyFinancialReportRow? FindRow(List<HmoneyFinancialReportRow> rows, string name)
        => rows.FirstOrDefault(r => r.Name != null &&
            r.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static string? FormatPeriod(HmoneyReportPeriod period)
    {
        if (period.Year == null) return null;
        // quarter = 0 nghĩa là số liệu cả năm, không phải quý 0.
        return period.Quarter is null or 0
            ? period.Year.Value.ToString()
            : $"Q{period.Quarter.Value}/{period.Year.Value}";
    }

    /// <summary>
    /// Epoch giây từ nguồn neo vào nửa đêm giờ Việt Nam, nên format theo UTC sẽ lùi đúng một ngày.
    /// </summary>
    private static string? FormatVietnamDate(long? epochSeconds)
    {
        if (epochSeconds is null or 0) return null;
        return DateTimeOffset.FromUnixTimeSeconds(epochSeconds.Value)
            .ToOffset(VietnamOffset)
            .ToString("dd/MM/yyyy");
    }

    /// <summary>
    /// Cố ý KHÔNG dùng <c>NumberStyles.Any</c>: nó cho phép dấu phân cách nghìn, nên nếu nguồn đổi
    /// sang cách viết Việt Nam thì "16,07" đọc thành 1607 — sai gấp 100 lần mà vẫn im lặng.
    /// <c>Float</c> làm chuỗi đó hỏng parse và rơi về null, dễ thấy hơn nhiều.
    /// </summary>
    private static decimal? ParseDecimal(string? raw)
        => decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
