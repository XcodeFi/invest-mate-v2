namespace InvestmentApp.Domain.Constants;

/// <summary>
/// Rổ VN30 (cập nhật Q1/2026), nguồn duy nhất: nhập danh sách theo dõi VN30 và tỉ lệ nền của setup tín hiệu cùng dùng.
/// Đây là thành phần HIỆN TẠI của rổ, không phải lịch sử thành viên (thiên lệch người sống sót được nêu rõ ở nơi hiển thị).
/// </summary>
public static class Vn30Basket
{
    public static IReadOnlyList<string> Symbols { get; } = new[]
    {
        "ACB", "BCM", "BID", "BVH", "CTG",
        "FPT", "GAS", "GVR", "HDB", "HPG",
        "MBB", "MSN", "MWG", "PLX", "POW",
        "SAB", "SHB", "SSB", "SSI", "STB",
        "TCB", "TPB", "VCB", "VHM", "VIB",
        "VIC", "VJC", "VNM", "VPB", "VRE"
    };

    private static readonly HashSet<string> Set = new(Symbols, StringComparer.Ordinal);

    public static bool IsMember(string? symbol)
        => !string.IsNullOrWhiteSpace(symbol) && Set.Contains(symbol.Trim().ToUpperInvariant());
}
