using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using InvestmentApp.Application.MarketData.Screener;

namespace InvestmentApp.Infrastructure.Services.Screener;

public sealed record HoseStatusListInfo(int Id, string Name);
public sealed record HoseStatusPage(int TotalPages, IReadOnlyList<HoseStatusEntry> Entries);

/// <summary>
/// Đọc JSON công khai của HOSE: danh sách các loại diện đặc biệt, từng mã trong một danh sách, và các mã hủy niêm yết.
/// Lý do trả về dạng HTML nên được bóc thành văn bản thuần ở đây (phía server); giao diện chỉ hiển thị như text.
/// </summary>
public static class HoseStatusParser
{
    /// <summary>Các danh sách cần đọc: 33–38 (cảnh báo, kiểm soát, tạm ngừng, đình chỉ, kiểm soát + tạm ngừng, hạn chế) và 47–53.</summary>
    public static readonly IReadOnlySet<int> WantedListIds = new HashSet<int> { 33, 34, 35, 36, 37, 38, 47, 48, 49, 50, 51, 52, 53 };

    private static readonly Regex Tag = new(@"</?[a-zA-Z][^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Blank = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Script = new(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex StockCode = new("^[A-Z0-9]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BlockEnd = new(@"</(p|div|li|tr|h\d)\s*>|<br\s*/?>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<HoseStatusListInfo> ParseStatusLists(string json)
    {
        using var doc = Open(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new FormatException("HOSE status-list: thiếu data là mảng.");
        var result = new List<HoseStatusListInfo>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && WantedListIds.Contains(id.GetInt32()))
                result.Add(new HoseStatusListInfo(id.GetInt32(), Str(item, "name") ?? $"Danh sách {id.GetInt32()}"));
        }
        return result;
    }

    public static HoseStatusPage ParseStockStatus(string json, string listName)
    {
        using var doc = Open(json);
        var list = ListOf(doc.RootElement);
        var entries = new List<HoseStatusEntry>();
        foreach (var item in list.EnumerateArray())
        {
            var code = Str(item, "securitiesCode")?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) continue;
            entries.Add(new HoseStatusEntry(code, listName, StripHtml(Str(item, "reason")), UnixDate(item, "appliedDate")));
        }
        var totalPages = 1;
        if (doc.RootElement.GetProperty("data").TryGetProperty("paging", out var paging)
            && paging.TryGetProperty("totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number)
            totalPages = Math.Max(1, tp.GetInt32());
        return new HoseStatusPage(totalPages, entries);
    }

    /// <summary>Chỉ giữ mã cổ phiếu 3 ký tự; danh sách hủy niêm yết lẫn cả chứng quyền (mã dài hơn).</summary>
    public static IReadOnlyList<HoseDelisting> ParseDelistings(string json)
    {
        using var doc = Open(json);
        var list = ListOf(doc.RootElement);
        var result = new List<HoseDelisting>();
        foreach (var item in list.EnumerateArray())
        {
            var code = Str(item, "code")?.Trim().ToUpperInvariant();
            if (code != null && StockCode.IsMatch(code)) result.Add(new HoseDelisting(code, UnixDate(item, "cancelDate")));
        }
        return result;
    }

    /// <summary>Bóc HTML thành văn bản thuần: bỏ script/style, thẻ khối thành khoảng trắng, giải mã thực thể, gộp khoảng trắng.</summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Script.Replace(html, " ");
        text = BlockEnd.Replace(text, " ");
        text = Tag.Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        // Sau khi giải mã, một thực thể như &lt;script&gt; có thể thành thẻ thật: bỏ nốt. Dấu < đứng riêng (như "vốn < 0") là chữ, được giữ.
        text = Tag.Replace(text, "");
        return Blank.Replace(text, " ").Trim();
    }

    private static JsonDocument Open(string json)
    {
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { throw new FormatException("HOSE: phản hồi không phải JSON."); }
    }

    private static JsonElement ListOf(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
            return list;
        throw new FormatException("HOSE: phản hồi không đúng dạng mong đợi (data.list là mảng).");
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? UnixDate(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : null;
}
