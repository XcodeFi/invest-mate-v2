using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// System-level strategy template — không thuộc user nào.
/// Dùng làm gợi ý khi user tạo strategy mới.
/// </summary>
public class StrategyTemplate
{
    [BsonId]
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;         // ValueInvesting, Technical, PortfolioManagement
    public string Description { get; set; } = null!;
    public string Suggestion { get; set; } = string.Empty; // Gợi ý khi nào nên dùng
    public string EntryRules { get; set; } = string.Empty;
    public string ExitRules { get; set; } = string.Empty;
    public string RiskRules { get; set; } = string.Empty;
    public string TimeFrame { get; set; } = "Swing";       // Scalping, DayTrading, Swing, Position
    public string MarketCondition { get; set; } = "All";    // Trending, Ranging, Volatile, All
    public string DifficultyLevel { get; set; } = "Intermediate"; // Beginner, Intermediate, Advanced
    public List<string> SuitableFor { get; set; } = new();  // ["Nhà đầu tư dài hạn", "Người mới bắt đầu"]
    public List<string> KeyIndicators { get; set; } = new(); // ["P/E", "ROE", "MA50"]
    public List<string> Tags { get; set; } = new();         // ["value", "long-term", "fundamental"]

    // P5: Auto-fill fields for Trade Plan integration
    public decimal? SuggestedSlPercent { get; set; }         // % SL (VD: 5 = -5%)
    public decimal? SuggestedRrRatio { get; set; }           // R:R (VD: 2.0)
    public string? SuggestedSlMethod { get; set; }           // "manual" | "atr" | "chandelier" | "ma_trailing" | "support"
    public decimal? SuggestedAtrMultiplier { get; set; }     // ATR multiplier for SL (1.5, 2, 3)
    public string? SuggestedSizingModel { get; set; }        // "fixed_risk" | "atr_based" | "turtle" | "volatility_adjusted"

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
