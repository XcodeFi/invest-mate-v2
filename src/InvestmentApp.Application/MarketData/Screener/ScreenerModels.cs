namespace InvestmentApp.Application.MarketData.Screener;

/// <summary>
/// Một dòng của kho dữ liệu toàn thị trường (24hmoney technical-filter). Đơn vị đã chuẩn hoá khi đọc nguồn (xem <c>TechnicalFilterParser</c>):
/// vốn hoá và lợi nhuận tính bằng TỶ đồng, giá bằng đồng, khối lượng bằng cổ phiếu. <c>Rs*</c> là XẾP HẠNG PHÂN VỊ 1–99
/// (99 = mạnh hơn 99% số mã), KHÔNG phải tỉ suất sinh lời.
/// </summary>
public sealed record ScreenerRow(
    string Symbol,
    string Floor,
    string? IcbGroup,
    decimal MarketCapBn,
    decimal PriceVnd,
    decimal? AvgVolume10,
    decimal? Price10Vnd,
    decimal? Pe4Q,
    decimal? Pb4Q,
    decimal? Roe,
    decimal? Roa,
    decimal? ProfitAfterTaxBn,
    int? Rs1m,
    int? Rs3m,
    int? Rs52w,
    decimal? Beta)
{
    /// <summary>Giá trị giao dịch bình quân 10 phiên, tỷ đồng = KL TB 10 phiên × giá TB 10 phiên (rơi về giá hiện tại nếu thiếu).</summary>
    public decimal? AdtvBn
    {
        get
        {
            if (AvgVolume10 is not { } vol || vol < 0) return null;
            var price = Price10Vnd is > 0 ? Price10Vnd.Value : PriceVnd;
            return price > 0 ? vol * price / 1_000_000_000m : null;
        }
    }
}

/// <summary>Một mã nằm trong một danh sách diện đặc biệt của HOSE (cảnh báo, kiểm soát, hạn chế giao dịch, tạm ngừng...).</summary>
public sealed record HoseStatusEntry(string Symbol, string ListName, string Reason, DateTime? AppliedDate);

/// <summary>Mã HOSE đã có thông báo hủy niêm yết.</summary>
public sealed record HoseDelisting(string Symbol, DateTime? CancelDate);

/// <summary>Các danh sách diện đặc biệt và hủy niêm yết của HOSE ở một thời điểm.</summary>
public sealed record HoseLists(IReadOnlyList<HoseStatusEntry> Statuses, IReadOnlyList<HoseDelisting> Delistings);

/// <summary>Một lần lấy kho dữ liệu: các dòng, thời điểm lấy và các danh sách HOSE (null = chưa kiểm được, KHÔNG phải rỗng).</summary>
public sealed class ScreenerSnapshot
{
    public IReadOnlyList<ScreenerRow> Rows { get; init; } = Array.Empty<ScreenerRow>();
    public DateTimeOffset? AsOf { get; init; }
    public bool Stale { get; init; }
    public HoseLists? Hose { get; init; }
    public DateTimeOffset? HoseAsOf { get; init; }
    public bool HoseStale { get; init; }

    /// <summary>Có giá trị khi không có dữ liệu dùng được (chưa lấy được lần nào, hoặc đã cũ quá 48 giờ).</summary>
    public string? UnavailableReason { get; init; }

    public List<string> Notes { get; init; } = new();
}

/// <summary>Tiêu chí sàng lọc. Mặc định là bộ chốt trong ADR-0020; mọi giá trị đi qua <see cref="Clamped"/> trước khi dùng.</summary>
public sealed class ScreenerCriteria
{
    public static readonly string[] AllowedFloors = { "HOSE", "HNX", "UPCOM" };

    public IReadOnlyList<string> Floors { get; init; } = new[] { "HOSE", "HNX" };
    public bool IncludeUpcom { get; init; }
    public decimal MinAdtvBn { get; init; } = 5m;
    public decimal MinPriceVnd { get; init; } = 5_000m;
    public decimal MinMarketCapBn { get; init; } = 1_000m;
    public decimal MinRoe { get; init; } = 12m;

    /// <summary>Trần P/E; mặc định TẮT (null): P/E cao không phải lý do loại ngầm định.</summary>
    public decimal? MaxPe { get; init; }

    /// <summary>Xếp hạng sức mạnh 3 tháng tối thiểu (P1–P99, trong toàn bộ mã niêm yết); 50 = trung vị.</summary>
    public int MinRs3m { get; init; } = 50;

    public bool SkipQualityLayer { get; init; }
    public bool IncludeExcluded { get; init; }
    public int ExcludedSkip { get; init; }
    public int ExcludedTake { get; init; } = DefaultExcludedTake;
    public string? LookupSymbol { get; init; }

    public const int DefaultExcludedTake = 100;
    public const int MaxExcludedTake = 200;

    /// <summary>Chỉ nhận đúng các giá trị HOSE, HNX, UPCOM (không phân biệt hoa thường, bỏ trùng); giá trị khác là lỗi người gọi.</summary>
    public static IReadOnlyList<string> ParseFloors(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new[] { "HOSE", "HNX" };
        var result = new List<string>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var floor = part.ToUpperInvariant();
            if (!AllowedFloors.Contains(floor))
                throw new ArgumentException($"Sàn \"{part}\" không hợp lệ. Chỉ nhận HOSE, HNX, UPCOM (cách nhau bởi dấu phẩy), ví dụ HOSE,HNX.", nameof(csv));
            if (!result.Contains(floor)) result.Add(floor);
        }
        return result.Count == 0 ? new[] { "HOSE", "HNX" } : result;
    }

    /// <summary>Đưa mọi ngưỡng về khoảng hợp lý: giá trị vô lý (âm, quá lớn) không bao giờ làm hỏng lần lọc hay đòi thêm dữ liệu nguồn.</summary>
    public ScreenerCriteria Clamped() => new()
    {
        Floors = Floors.Select(f => f.ToUpperInvariant()).Where(f => AllowedFloors.Contains(f)).Distinct().ToArray(),
        IncludeUpcom = IncludeUpcom,
        MinAdtvBn = Math.Clamp(MinAdtvBn, 0m, 10_000m),
        MinPriceVnd = Math.Clamp(MinPriceVnd, 0m, 1_000_000m),
        MinMarketCapBn = Math.Clamp(MinMarketCapBn, 0m, 10_000_000m),
        MinRoe = Math.Clamp(MinRoe, -100m, 100m),
        MaxPe = MaxPe is { } pe ? Math.Clamp(pe, 0m, 1_000m) : null,
        MinRs3m = Math.Clamp(MinRs3m, 1, 99),
        SkipQualityLayer = SkipQualityLayer,
        IncludeExcluded = IncludeExcluded,
        ExcludedSkip = Math.Max(0, ExcludedSkip),
        ExcludedTake = Math.Clamp(ExcludedTake, 1, MaxExcludedTake),
        LookupSymbol = string.IsNullOrWhiteSpace(LookupSymbol) ? null : LookupSymbol.Trim().ToUpperInvariant()
    };
}

/// <summary>Một mã vượt qua bộ lọc. Cố ý KHÔNG có điểm số hay thứ hạng: danh sách chỉ là tập thoả tiêu chí.</summary>
public sealed class ScreenerPassedRow
{
    public string Symbol { get; init; } = "";
    public string Floor { get; init; } = "";
    public string? Sector { get; init; }
    public decimal? AdtvBn { get; init; }
    public decimal MarketCapBn { get; init; }
    public decimal PriceVnd { get; init; }
    public decimal? Roe { get; init; }
    public decimal? Pe { get; init; }
    public int? Rs3m { get; init; }

    /// <summary>Lý do đạt (vượt từng tầng nào).</summary>
    public List<string> Reasons { get; init; } = new();

    /// <summary>Cờ biến động (dựa trên xếp hạng tương đối): chỉ để người dùng cân nhắc, KHÔNG loại mã.</summary>
    public List<string> Flags { get; init; } = new();

    /// <summary>Ghi chú riêng của dòng, ví dụ chưa kiểm được danh sách cảnh báo HNX.</summary>
    public List<string> Notes { get; init; } = new();
}

public sealed class ScreenerExcludedRow
{
    public string Symbol { get; init; } = "";
    public string Floor { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class ScreenerSectorCount
{
    public int Passed { get; set; }
    public int Excluded { get; set; }
}

/// <summary>Kết quả tra "Vì sao mã X không có?".</summary>
public sealed class ScreenerLookup
{
    public string Symbol { get; init; } = "";

    /// <summary><c>passed</c>, <c>unverified_hose</c>, <c>excluded</c> hoặc <c>not_in_universe</c>.</summary>
    public string State { get; init; } = "";
    public string? Reason { get; init; }
}

public sealed class ScreenerResult
{
    public const string DisclaimerText = "Danh sách lọc theo tiêu chí, không phải khuyến nghị mua. Dữ liệu từ nguồn công khai không chính thức, có thể trễ. Cờ dựa trên xếp hạng tương đối, không phải đánh giá rủi ro.";

    /// <summary><c>ok</c>, <c>stale</c> (đang dùng dữ liệu cũ vì làm mới lỗi) hoặc <c>unavailable</c> (không có dữ liệu dùng được).</summary>
    public string Status { get; init; } = "ok";
    public bool Stale { get; init; }
    public DateTimeOffset? AsOf { get; init; }
    public DateTimeOffset? HoseListsAsOf { get; init; }
    public bool HoseListsStale { get; init; }
    public string? Reason { get; init; }
    public int UniverseCount { get; init; }

    public List<ScreenerPassedRow> Passed { get; init; } = new();

    /// <summary>Mã HOSE qua mọi tầng nhưng chưa kiểm được danh sách diện đặc biệt/hủy niêm yết: KHÔNG nằm trong <see cref="Passed"/>.</summary>
    public List<ScreenerPassedRow> UnverifiedHose { get; init; } = new();

    public Dictionary<string, int> ExcludedCounts { get; init; } = new();
    public Dictionary<string, ScreenerSectorCount> SectorCounts { get; init; } = new();

    /// <summary>Số mã chỉ bị loại bởi tầng chất lượng/trần P/E trong khi xu hướng và thanh khoản đạt ("loại vì định giá/chất lượng nhưng xu hướng tốt").</summary>
    public int TradeoffCount { get; init; }

    public int? ExcludedTotal { get; init; }
    public List<ScreenerExcludedRow>? Excluded { get; init; }
    public ScreenerLookup? Lookup { get; init; }
    public List<string> Notes { get; init; } = new();
    public string Disclaimer { get; init; } = DisclaimerText;
}
