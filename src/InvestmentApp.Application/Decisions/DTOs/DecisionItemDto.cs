namespace InvestmentApp.Application.Decisions.DTOs;

/// <summary>
/// Loại quyết định mà user cần xử lý — gộp 5 nguồn alert thành 1 queue duy nhất ở Dashboard:
/// 3 nguồn phòng thủ (StopLoss / Scenario trigger / Thesis review) + 2 nguồn phía vào lệnh
/// (Cơ hội mua / Thiếu stop-loss). Xem `docs/plans/dashboard-decision-engine.md` §5 (P3)
/// và `docs/adr/0009-decision-queue-entry-side-signals.md`.
/// </summary>
public enum DecisionType
{
    /// <summary>Vị thế đã chạm hoặc xuyên qua stop-loss.</summary>
    StopLossHit,

    /// <summary>Scenario node của TradePlan đã trigger (e.g. giá ≥ 80k → bán 30%).</summary>
    ScenarioTrigger,

    /// <summary>Thesis hết hạn review hoặc invalidation rule đến hạn check.</summary>
    ThesisReviewDue,

    /// <summary>Mã trong watchlist có giá ≤ mục tiêu mua — cơ hội vào lệnh.</summary>
    BuyOpportunity,

    /// <summary>Vị thế đang mở nhưng chưa đặt stop-loss — rủi ro chưa được giới hạn.</summary>
    MissingStopLoss
}

public enum DecisionSeverity
{
    /// <summary>Cần hành động ngay (SL bị xuyên thủng, thesis quá hạn ≥ 3 ngày).</summary>
    Critical,

    /// <summary>Cần để ý (gần SL, thesis sắp đến hạn, scenario trigger).</summary>
    Warning,

    /// <summary>Thông tin — cơ hội mua, xếp dưới mọi cảnh báo rủi ro.</summary>
    Info
}

/// <summary>
/// 1 item trong Decision Queue. View-model thuần — không persist.
/// Id là composite "{type}:{sourceId}" (sourceId = tradePlanId hoặc symbol).
/// </summary>
public class DecisionItemDto
{
    public string Id { get; set; } = null!;
    public DecisionType Type { get; set; }
    public DecisionSeverity Severity { get; set; }
    public string Symbol { get; set; } = null!;
    public string PortfolioId { get; set; } = string.Empty;
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>Tóm tắt 1 dòng để render trên card (e.g. "FPT chạm SL 89.5 (giá 89.4)").</summary>
    public string Headline { get; set; } = null!;

    /// <summary>Lý do gốc (thesis hoặc trigger reason). Hiển thị ở phần phụ của card.</summary>
    public string? ThesisOrReason { get; set; }

    public decimal? CurrentPrice { get; set; }
    public decimal? PlannedExitPrice { get; set; }

    /// <summary>
    /// TradePlanId nếu item bắt nguồn từ một kế hoạch: ScenarioTrigger và ThesisReviewDue luôn có.
    /// StopLossHit / MissingStopLoss / BuyOpportunity luôn null — chúng sinh từ vị thế hoặc watchlist,
    /// không từ plan. Đừng dùng trường này để nhận diện phạm vi danh mục; dùng <see cref="PortfolioId"/>.
    /// </summary>
    public string? TradePlanId { get; set; }

    /// <summary>Hạn xử lý (cho ThesisReviewDue) hoặc thời điểm trigger (StopLoss/Scenario).</summary>
    public DateTime? DueAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class DecisionQueueDto
{
    public IReadOnlyList<DecisionItemDto> Items { get; set; } = Array.Empty<DecisionItemDto>();
    public int TotalCount { get; set; }
}
