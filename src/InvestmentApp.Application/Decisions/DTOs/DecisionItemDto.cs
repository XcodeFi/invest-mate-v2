namespace InvestmentApp.Application.Decisions.DTOs;

/// <summary>
/// Loại quyết định mà user cần xử lý — gộp 3 nguồn alert (StopLoss / Scenario trigger / Thesis review)
/// thành 1 queue duy nhất ở Dashboard. Xem `docs/plans/dashboard-decision-engine.md` §5 (P3).
/// </summary>
public enum DecisionType
{
    /// <summary>Vị thế đã chạm hoặc xuyên qua stop-loss.</summary>
    StopLossHit,

    /// <summary>Scenario node của TradePlan đã trigger (e.g. giá ≥ 80k → bán 30%).</summary>
    ScenarioTrigger,

    /// <summary>Thesis hết hạn review hoặc invalidation rule đến hạn check.</summary>
    ThesisReviewDue
}

public enum DecisionSeverity
{
    /// <summary>Cần hành động ngay (SL bị xuyên thủng, thesis quá hạn ≥ 3 ngày).</summary>
    Critical,

    /// <summary>Cần để ý (gần SL, thesis sắp đến hạn, scenario trigger).</summary>
    Warning,

    /// <summary>Thông tin (reserved cho V2).</summary>
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

    /// <summary>TradePlanId nếu source có plan (StopLossHit/ScenarioTrigger/ThesisReviewDue đều có).</summary>
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
