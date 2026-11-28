using InvestmentApp.Application.MarketData.Screener;

namespace InvestmentApp.Application.Interfaces;

/// <summary>Kết quả một lần lấy toàn bộ thị trường từ nguồn: các dòng hợp lệ cùng số đo để cổng chất lượng kiểm tra.</summary>
public sealed record ScreenerUniverseFetch(IReadOnlyList<ScreenerRow> Rows, int RawCount, int InvalidCount, int RsOutOfRangeCount);

/// <summary>
/// Nguồn dữ liệu của bộ sàng lọc. Mỗi hàm là "tất cả hoặc không gì cả": thiếu một trang là ném lỗi, không bao giờ trả một phần.
/// </summary>
public interface IScreenerDataSource
{
    /// <summary>Toàn bộ mã HOSE + HNX + UPCOM (một khoá lấy duy nhất, ngưỡng lọc không tham gia).</summary>
    Task<ScreenerUniverseFetch> FetchUniverseAsync(CancellationToken ct = default);

    /// <summary>Các danh sách diện đặc biệt và hủy niêm yết của HOSE.</summary>
    Task<HoseLists> FetchHoseListsAsync(CancellationToken ct = default);
}

/// <summary>
/// Cung cấp kho dữ liệu đã đệm (6 giờ, một lần làm mới tại một thời điểm), giữ bản tốt gần nhất khi nguồn lỗi (tối đa 48 giờ).
/// </summary>
public interface IMarketScreenerService
{
    Task<ScreenerSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
