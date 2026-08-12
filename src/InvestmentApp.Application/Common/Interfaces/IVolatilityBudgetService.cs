namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Trần khối lượng theo ngân sách biến động cho một lệnh mua dự kiến (ADR-0014).
/// </summary>
public interface IVolatilityBudgetService
{
    Task<VolatilitySizingResult> GetSizingForPlanAsync(
        string portfolioId, string symbol, decimal entryPrice, int quantity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Chất lượng dữ liệu đằng sau con số. Người gọi phải phân biệt: <see cref="Insufficient"/> thì
/// mọi trường số là <c>null</c> và giao diện không được hiện con số nào.
/// </summary>
public enum VolatilityDataQuality
{
    /// <summary>Mọi mã đủ quan sát, không mã nào bị loại quan sát bất thường.</summary>
    Full,

    /// <summary>Thiếu lịch sử một vài mã đang giữ, hoặc có mã bị loại quan sát. Số vẫn tính được.</summary>
    Partial,

    /// <summary>Mã đang xét không đủ lịch sử. Không có con số nào dùng được.</summary>
    Insufficient
}

/// <summary>
/// Mọi trường phần trăm nullable <b>có chủ ý</b>: trả 0 cho đại lượng chưa tính được là lẫn "bằng
/// không" với "chưa biết" — cùng nguyên tắc đã áp cho <c>SectorExposureForPlan</c>.
/// </summary>
public class VolatilitySizingResult
{
    public string Symbol { get; set; } = null!;

    /// <summary>Biến động của phần vốn ĐANG ĐẦU TƯ, %/năm. Không gồm tiền mặt — xem ADR-0014.</summary>
    public decimal? CurrentVolatilityPercent { get; set; }

    public decimal? ProjectedVolatilityPercent { get; set; }

    /// <summary>Suy từ <c>RiskProfile.MaxDrawdownAlertPercent</c>. Luôn phải hiện lên giao diện.</summary>
    public decimal BudgetVolatilityPercent { get; set; }

    /// <summary>Giá trị nguồn của ngân sách, để giao diện giải thích được con số đến từ đâu.</summary>
    public decimal SourceMaxDrawdownPercent { get; set; }

    public decimal? CorrelationWithPortfolio { get; set; }
    public decimal? MarginalRiskContributionPercent { get; set; }
    public decimal? CapitalWeightPercent { get; set; }

    /// <summary>Trần khối lượng. <c>null</c> khi không tính được HOẶC không bị ràng buộc — phân biệt
    /// bằng <see cref="IsUnconstrainedByVolatility"/>.</summary>
    public int? MaxQuantityWithinBudget { get; set; }

    /// <summary>Mã hiền hơn ngân sách: mua bao nhiêu cũng không đẩy vượt. Khác hẳn "không tính được".</summary>
    public bool IsUnconstrainedByVolatility { get; set; }

    public bool PortfolioAlreadyOverBudget { get; set; }

    public VolatilityDataQuality DataQuality { get; set; }

    /// <summary>Mã đang giữ không đủ lịch sử, nên không nằm trong ước lượng.</summary>
    public List<string> MissingSymbols { get; set; } = new();

    /// <summary>Mã bị loại quan sát bất thường (nghi sự kiện quyền chưa điều chỉnh).</summary>
    public List<string> AdjustedSymbols { get; set; } = new();

    /// <summary>
    /// Mã mà việc LẤY lịch sử giá hỏng (mạng, lệch hợp đồng nguồn) — khác hẳn mã thật sự chưa đủ
    /// lịch sử. Gộp hai ca này là nói với người dùng "mã này chưa đủ lịch sử" trong khi sự thật là
    /// "chúng tôi không lấy được", và họ sẽ kết luận sai rằng mã đó mới hoặc thanh khoản kém.
    /// </summary>
    public List<string> FetchFailedSymbols { get; set; } = new();

    /// <summary>Số phiên trong cửa sổ chung. Hiện lên để người dùng tự chiết khấu độ tin cậy.</summary>
    public int ObservationCount { get; set; }
}
