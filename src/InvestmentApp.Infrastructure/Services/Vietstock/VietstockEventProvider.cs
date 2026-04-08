using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services.Vietstock;

public class VietstockEventProvider : IVietstockEventProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VietstockEventProvider> _logger;
    private string? _csrfToken;
    private DateTime _csrfTokenExpiry = DateTime.MinValue;
    private static readonly Regex DateRegex = new(@"/Date\((\d+)\)/", RegexOptions.Compiled);
    private static readonly Regex CsrfInputRegex = new(
        @"name=""?__RequestVerificationToken""?\s[^>]*value=""?([^""\s>]+)", RegexOptions.Compiled);
    private static readonly Regex CsrfFallbackRegex = new(
        @"__CHART_AjaxAntiForgeryForm[^<]*<input[^>]*value=([^\s>""]+)", RegexOptions.Compiled);
    private static readonly Regex CsrfLastResortRegex = new(
        @"__RequestVerificationToken[^>]*value=(?:""?)([^""\s>]+)", RegexOptions.Compiled);

    private const string BaseUrl = "https://finance.vietstock.vn";
    private const string PageUrl = "https://vietstock.vn";

    public VietstockEventProvider(HttpClient httpClient, ILogger<VietstockEventProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<VietstockNewsDto>> GetNewsAsync(
        string symbol, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        await EnsureCsrfTokenAsync(symbol, ct);

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = symbol.ToUpper(),
            ["type"] = "-1",
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["__RequestVerificationToken"] = _csrfToken!
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/data/GetNews")
        {
            Content = formData
        };
        request.Headers.Add("Referer", $"{BaseUrl}/{symbol.ToLower()}/tin-tuc-su-kien.htm");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        var items = JsonSerializer.Deserialize<List<VietstockNewsRaw>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (items == null) return Enumerable.Empty<VietstockNewsDto>();

        return items.Select(item => new VietstockNewsDto
        {
            StockCode = item.StockCode ?? symbol.ToUpper(),
            ArticleId = item.ArticleID,
            Title = item.Title ?? "",
            Head = item.Head,
            PublishTime = ParseDotNetDate(item.PublishTime),
            Url = item.URL != null ? $"{PageUrl}{item.URL}" : null,
            Source = item.Source
        });
    }

    public async Task<IEnumerable<VietstockEventDto>> GetEventsAsync(
        string symbol, int eventTypeId = 1, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        await EnsureCsrfTokenAsync(symbol, ct);

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["eventTypeID"] = eventTypeId.ToString(),
            ["channelID"] = "0",
            ["code"] = symbol.ToUpper(),
            ["catID"] = "-1",
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["orderBy"] = "Date1",
            ["orderDir"] = "DESC",
            ["__RequestVerificationToken"] = _csrfToken!
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/data/EventsTypeData")
        {
            Content = formData
        };
        request.Headers.Add("Referer", $"{BaseUrl}/{symbol.ToLower()}/tin-tuc-su-kien.htm");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        // Response format: [[{data}], [totalCount]]
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 1)
            return Enumerable.Empty<VietstockEventDto>();

        var dataArray = root[0];
        if (dataArray.ValueKind != JsonValueKind.Array)
            return Enumerable.Empty<VietstockEventDto>();

        var results = new List<VietstockEventDto>();
        foreach (var item in dataArray.EnumerateArray())
        {
            results.Add(new VietstockEventDto
            {
                EventId = item.GetProperty("EventID").GetInt64(),
                EventTypeId = item.GetProperty("EventTypeID").GetInt32(),
                ChannelId = item.TryGetProperty("ChannelID", out var ch) ? ch.GetInt32() : 0,
                Code = item.TryGetProperty("Code", out var code) ? code.GetString() ?? symbol : symbol,
                Name = item.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                Note = item.TryGetProperty("Note", out var note) ? note.GetString() : null,
                Title = item.TryGetProperty("Title", out var title) ? title.GetString() : null,
                EventDate = ParseDotNetDate(
                    item.TryGetProperty("GDKHQDate", out var date) ? date.GetString() : null),
                FileUrl = item.TryGetProperty("FileUrl", out var file) ? file.GetString() : null
            });
        }

        return results;
    }

    internal async Task EnsureCsrfTokenAsync(string symbol, CancellationToken ct)
    {
        if (_csrfToken != null && DateTime.UtcNow < _csrfTokenExpiry) return;

        _logger.LogInformation("Refreshing Vietstock CSRF token for {Symbol}", symbol);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/{symbol.ToLower()}/tin-tuc-su-kien.htm");
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);

        // Parse token from hidden input — try multiple patterns
        // Pattern 1: with quotes: value="token"
        // Pattern 2: without quotes: value=token>
        var match = CsrfInputRegex.Match(html);
        if (!match.Success)
        {
            var fallback = CsrfFallbackRegex.Match(html);
            if (fallback.Success)
            {
                match = fallback;
            }
            else
            {
                var lastResort = CsrfLastResortRegex.Match(html);
                if (lastResort.Success)
                    match = lastResort;
            }
        }

        if (!match.Success)
        {
            var sample = html.Length > 500 ? html.Substring(0, 500) : html;
            _logger.LogWarning("Failed to parse CSRF token from Vietstock HTML. Sample: {Sample}", sample);
            throw new InvalidOperationException("Cannot obtain Vietstock CSRF token");
        }

        _csrfToken = match.Groups[1].Value;
        _csrfTokenExpiry = DateTime.UtcNow.AddMinutes(30);
        _logger.LogInformation("Obtained Vietstock CSRF token ({Length} chars)", _csrfToken.Length);
    }

    public static DateTime ParseDotNetDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;

        var match = DateRegex.Match(dateStr);
        if (!match.Success) return DateTime.MinValue;

        var ms = long.Parse(match.Groups[1].Value);
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
    }

    // Raw JSON models for deserialization
    private class VietstockNewsRaw
    {
        public string? StockCode { get; set; }
        public long ArticleID { get; set; }
        public string? Title { get; set; }
        public string? Head { get; set; }
        public string? PublishTime { get; set; }
        public string? URL { get; set; }
        public string? Source { get; set; }
    }
}
