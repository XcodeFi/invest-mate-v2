using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Screener;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Services.Screener;

/// <summary>
/// Nguồn dữ liệu sàng lọc: technical-filter của 24hmoney (cùng họ API và cùng dạng yêu cầu với <see cref="HmoneyMarketDataProvider"/>,
/// KHÔNG thêm header, User-Agent hay Referer nào) và các danh sách JSON công khai của HOSE (đo 2026-11-28: gọi được không cần header <c>type</c>).
/// Mỗi hàm là tất cả-hoặc-không: một trang lỗi hay sai dạng là ném lỗi, không bao giờ trả một phần.
/// </summary>
public class HmoneyScreenerDataSource : IScreenerDataSource
{
    /// <summary>Trần số trang: toàn thị trường hiện 4 trang × 500; con số vô lý từ nguồn không được kéo thành vòng lặp dài.</summary>
    private const int MaxPages = 20;
    private const int HosePageSize = 200;
    private const int MaxHosePages = 20;

    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly ScreenerOptions _options;

    public HmoneyScreenerDataSource(HttpClient http, TimeProvider time, IOptions<ScreenerOptions> options)
    {
        _http = http;
        _time = time;
        _options = options.Value;
    }

    public async Task<ScreenerUniverseFetch> FetchUniverseAsync(CancellationToken ct = default)
    {
        var rows = new List<ScreenerRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int raw = 0, invalid = 0, rsOut = 0;

        var page = 1;
        var totalPages = 1;
        while (page <= totalPages)
        {
            var parsed = TechnicalFilterParser.Parse(await GetAsync(UniversePath(page), ct));
            if (page == 1)
            {
                if (parsed.TotalPages > MaxPages)
                    throw new FormatException($"technical-filter: số trang {parsed.TotalPages} vượt trần {MaxPages}.");
                totalPages = parsed.TotalPages;
            }
            foreach (var row in parsed.Rows)
                if (seen.Add(row.Symbol)) rows.Add(row);
            raw += parsed.RawCount;
            invalid += parsed.InvalidCount;
            rsOut += parsed.RsOutOfRangeCount;
            page++;
        }
        return new ScreenerUniverseFetch(rows, raw, invalid, rsOut);
    }

    public async Task<HoseLists> FetchHoseListsAsync(CancellationToken ct = default)
    {
        var baseUrl = _options.HsxBaseUrl.TrimEnd('/');
        var lists = HoseStatusParser.ParseStatusLists(await GetAsync($"{baseUrl}/l/api/v1/securities/status-list", ct));
        // Không còn danh sách nào trong các id cần đọc nghĩa là nguồn đổi dạng hoặc đổi id: ném lỗi để đi đường cũ/đóng, không coi là "sạch".
        if (lists.Count == 0)
            throw new FormatException("HOSE status-list: không có danh sách nào trong các id cần đọc (33–38, 47–53); nguồn có thể đã đổi dạng.");

        var statuses = new List<HoseStatusEntry>();
        foreach (var list in lists)
        {
            var pageIndex = 1;
            var totalPages = 1;
            while (pageIndex <= totalPages)
            {
                var body = await GetAsync(
                    $"{baseUrl}/l/api/v1/1/securities/stock-status?pageIndex={pageIndex}&pageSize={HosePageSize}&statusListId={list.Id}", ct);
                var parsed = HoseStatusParser.ParseStockStatus(body, list.Name);
                if (pageIndex == 1)
                {
                    if (parsed.TotalPages > MaxHosePages)
                        throw new FormatException($"HOSE stock-status: số trang {parsed.TotalPages} vượt trần {MaxHosePages}.");
                    totalPages = parsed.TotalPages;
                }
                statuses.AddRange(parsed.Entries);
                pageIndex++;
            }
        }

        // HOSE luôn có mã trong diện đặc biệt (≈ 50 mã đo 2026-11-28): tổng bằng 0 là dữ liệu hỏng, không phải "không mã nào bị cảnh báo".
        if (statuses.Count == 0)
            throw new FormatException("HOSE stock-status: không có mã nào trong các danh sách diện đặc biệt; nguồn có thể đã đổi dạng.");

        var year = _time.GetUtcNow().Year;
        var delistings = new List<HoseDelisting>();
        foreach (var y in new[] { year, year + 1 })
            delistings.AddRange(HoseStatusParser.ParseDelistings(await GetAsync($"{baseUrl}/l/api/v1/1/securities/cancel?year={y}", ct)));

        return new HoseLists(statuses, delistings.GroupBy(d => d.Symbol, StringComparer.Ordinal).Select(g => g.First()).ToList());
    }

    private static string UniversePath(int page)
        => "/v1/ios/company/technical-filter?" + HmoneyMarketDataProvider.CommonParams +
           $"&floor=all&page={page}&per_page={TechnicalFilterParser.PageSize}" +
           $"&param={Uri.EscapeDataString(TechnicalFilterParser.FullUniverseParam)}&key=symbol&sort=asc";

    /// <summary>GET trần, không header bổ sung. Mã khác 2xx là HttpRequestException (thân HTML của WAF cũng vào đây hoặc thành FormatException khi đọc).</summary>
    private async Task<string> GetAsync(string urlOrPath, CancellationToken ct)
    {
        using var response = await _http.GetAsync(urlOrPath, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
