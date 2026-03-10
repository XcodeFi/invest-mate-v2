using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// System-level risk profile template — gợi ý cấu hình rủi ro theo mức độ chấp nhận.
/// </summary>
public class RiskProfileTemplate
{
    [BsonId]
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;              // "Bảo thủ", "Cân bằng", "Tích cực", "Mạo hiểm"
    public string Description { get; set; } = null!;
    public string Suggestion { get; set; } = string.Empty;  // Gợi ý ai nên dùng
    public decimal MaxPositionSizePercent { get; set; }
    public decimal MaxSectorExposurePercent { get; set; }
    public decimal MaxDrawdownAlertPercent { get; set; }
    public decimal DefaultRiskRewardRatio { get; set; }
    public decimal MaxPortfolioRiskPercent { get; set; }
    public List<string> SuitableFor { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
