using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// User-specific trade plan template. Lưu thiết lập kế hoạch giao dịch để tái sử dụng.
/// </summary>
public class TradePlanTemplate
{
    [BsonId]
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;           // "VNM Swing Entry", "DCA Breakout"
    public string? Symbol { get; set; }                  // null = generic template
    public string Direction { get; set; } = "Buy";       // "Buy" | "Sell"
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? Target { get; set; }
    public string? StrategyId { get; set; }
    public string MarketCondition { get; set; } = "Trending";
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
