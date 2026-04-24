namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Provides current top-rate-per-term snapshot for Vietnamese savings accounts.
/// Implemented by HmoneyBankRateProvider (24hmoney.vn/lai-suat-gui-ngan-hang HTML scrape).
/// </summary>
public interface IBankRateProvider
{
    /// <summary>
    /// Lấy lãi suất cao nhất cho mỗi kỳ hạn chuẩn (1/3/6/9/12 tháng) từ kênh **gửi online** (thường cao hơn quầy 0.2-0.8%).
    /// Fallback stale cache khi 24hmoney down.
    /// </summary>
    Task<BankRateSnapshot> GetTopRatesAsync(CancellationToken ct = default);
}

/// <summary>
/// Snapshot of top bank rates at fetch time.
/// </summary>
/// <param name="TopByTerm">Map từ kỳ hạn (tháng) → entry có lãi suất cao nhất. Keys: 1, 3, 6, 9, 12.</param>
/// <param name="SourceTimestamp">Timestamp từ trang 24hmoney (dòng "Cập nhật lúc: ..."). Null nếu không parse được.</param>
/// <param name="FetchedAt">Thời điểm crawler fetch data (UTC).</param>
public record BankRateSnapshot(
    IReadOnlyDictionary<int, BankRateEntry> TopByTerm,
    DateTime? SourceTimestamp,
    DateTime FetchedAt);

/// <summary>Entry cho 1 kỳ hạn: tên ngân hàng + lãi suất %/năm.</summary>
/// <param name="TermMonths">Kỳ hạn tính bằng tháng (1, 3, 6, 9, 12).</param>
/// <param name="RatePercent">Lãi suất %/năm dạng decimal (VD: 7.2m = 7.2%/năm).</param>
/// <param name="BankName">Tên ngân hàng hiển thị (VD: "PGBank", "VIB").</param>
public record BankRateEntry(int TermMonths, decimal RatePercent, string BankName);
