namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>
/// Luật giá của sàn dùng cho mô phỏng: biên độ trong ngày, bước giá và dung sai so sánh với trần/sàn.
/// Chuỗi đã điều chỉnh theo sự kiện quyền không nằm đúng trên bước giá nên không so sánh tuyệt đối được: mọi phép so dùng
/// <see cref="Slack"/> = max(0,001; 1 bước giá ÷ giá), tức "lệch tối đa một bước giá".
/// </summary>
public sealed class MarketRules
{
    /// <summary>Biên độ giá trong ngày: HOSE 7%, HNX 10%, UPCOM 15%.</summary>
    public decimal Limit { get; }

    private readonly bool _hose;

    private MarketRules(decimal limit, bool hose)
    {
        Limit = limit;
        _hose = hose;
    }

    /// <summary>Sàn không rõ hoặc thiếu thì coi như HOSE (7%, bước giá HOSE): giả định chặt nhất.</summary>
    public static MarketRules For(string? exchange)
    {
        // So khớp theo chứa: nguồn dữ liệu có thể trả "HNX", "Sàn HNX" hay "UPCOM"; không nhận ra thì 7%
        var name = exchange?.Trim().ToUpperInvariant() ?? "";
        if (name.Contains("UPCOM")) return new MarketRules(0.15m, hose: false);
        if (name.Contains("HNX")) return new MarketRules(0.10m, hose: false);
        return new MarketRules(0.07m, hose: true);   // HOSE, HSX, rỗng, lạ
    }

    /// <summary>
    /// Bước giá. HOSE: 10 đồng dưới 10.000, 50 đồng từ 10.000 đến dưới 50.000, 100 đồng từ 50.000. HNX và UPCOM: 100 đồng
    /// (mã nguồn chưa có bảng bước giá của hai sàn này nên giữ 100 đồng cho mọi mức giá).
    /// </summary>
    public decimal Tick(decimal price)
    {
        if (!_hose) return 100m;
        return price < 10_000m ? 10m : price < 50_000m ? 50m : 100m;
    }

    /// <summary>Dung sai tương đối: max(0,001; bước giá ÷ giá).</summary>
    public decimal Slack(decimal price) => price <= 0m ? 0.001m : Math.Max(0.001m, Tick(price) / price);

    /// <summary>Phiên tín hiệu đóng cửa ở trần hoặc sàn: |đóng / đóng hôm trước − 1| ≥ biên − slack.</summary>
    public bool IsAtLimit(decimal close, decimal prevClose)
        => prevClose > 0m && Math.Abs(close / prevClose - 1m) >= Limit - Slack(prevClose);

    /// <summary>Giá mở cửa phiên vào lệnh đã sát trần (≥ đóng cửa tín hiệu × (1 + biên − 1% − slack)): mua không khớp thực tế.</summary>
    public bool IsOpenNearCeiling(decimal open, decimal signalClose)
        => open >= signalClose * (1m + Limit - 0.01m - Slack(signalClose));

    /// <summary>Phiên bị khoá ở sàn (cao nhất chỉ bằng sàn cộng dung sai): không bán được.</summary>
    public bool IsFloorLocked(decimal high, decimal prevClose)
        => prevClose > 0m && high <= prevClose * (1m - Limit) * (1m + Slack(prevClose));
}
