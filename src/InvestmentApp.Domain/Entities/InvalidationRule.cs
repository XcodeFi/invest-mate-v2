namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Điều kiện khiến thesis bị phá vỡ (invalidation rule).
/// Khi <see cref="IsTriggered"/>=true nghĩa là user (hoặc hệ thống) đã xác nhận rule bị kích hoạt
/// → thesis sai, nên đóng lệnh (§D2, §D4 plan Vin-discipline).
/// </summary>
public class InvalidationRule
{
    public InvalidationTrigger Trigger { get; set; }

    /// <summary>
    /// Mô tả chi tiết khi nào rule bị vi phạm (tối thiểu 20 ký tự để đảm bảo falsifiable).
    /// Ví dụ: "BCTC Q1/2026 EPS < 20% YoY, HOẶC trích lập dự phòng > 2× Q trước".
    /// </summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// Ngày dự kiến verify rule (vd: ngày công bố BCTC, ngày earnings release).
    /// </summary>
    public DateTime? CheckDate { get; set; }

    /// <summary>
    /// True khi user xác nhận rule đã bị kích hoạt (thesis sai).
    /// </summary>
    public bool IsTriggered { get; set; }

    public DateTime? TriggeredAt { get; set; }
}

/// <summary>
/// 5 loại trigger cố định (§D2 plan Vin-discipline).
/// </summary>
public enum InvalidationTrigger
{
    /// <summary>KQKD không đạt kỳ vọng.</summary>
    EarningsMiss,

    /// <summary>Gãy trend kỹ thuật (vd: mất MA200, volume cao đỏ).</summary>
    TrendBreak,

    /// <summary>Tin tức thay đổi bản chất (CEO resign, scandal, regulation).</summary>
    NewsShock,

    /// <summary>Quá hạn mà thesis chưa thể hiện (vd: giữ 3 tháng vẫn sideways).</summary>
    ThesisTimeout,

    /// <summary>User tự nhận xét thesis sai.</summary>
    Manual
}
