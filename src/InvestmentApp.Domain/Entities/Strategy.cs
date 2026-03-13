using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Strategy aggregate root - defines trading strategy rules and classification.
/// Trades can be linked to a strategy for performance analysis.
/// </summary>
public class Strategy : AggregateRoot
{
    public string UserId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string EntryRules { get; private set; }
    public string ExitRules { get; private set; }
    public string RiskRules { get; private set; }
    public string TimeFrame { get; private set; }       // Scalping, Swing, Position
    public string MarketCondition { get; private set; }  // Trending, Ranging, Volatile
    public decimal? SuggestedSlPercent { get; private set; }  // % SL dưới giá vào (VD: 5 = -5%)
    public decimal? SuggestedRrRatio { get; private set; }    // R:R gợi ý (VD: 2.0 = TP = Entry + 2×Risk)
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public Strategy() { } // MongoDB

    public Strategy(string userId, string name, string description,
        string entryRules, string exitRules, string riskRules,
        string timeFrame, string marketCondition,
        decimal? suggestedSlPercent = null, decimal? suggestedRrRatio = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        EntryRules = entryRules ?? string.Empty;
        ExitRules = exitRules ?? string.Empty;
        RiskRules = riskRules ?? string.Empty;
        TimeFrame = timeFrame ?? "Swing";
        MarketCondition = marketCondition ?? "Trending";
        SuggestedSlPercent = suggestedSlPercent;
        SuggestedRrRatio = suggestedRrRatio;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? name = null, string? description = null,
        string? entryRules = null, string? exitRules = null,
        string? riskRules = null, string? timeFrame = null,
        string? marketCondition = null, bool? isActive = null,
        decimal? suggestedSlPercent = null, decimal? suggestedRrRatio = null)
    {
        if (name != null) Name = name;
        if (description != null) Description = description;
        if (entryRules != null) EntryRules = entryRules;
        if (exitRules != null) ExitRules = exitRules;
        if (riskRules != null) RiskRules = riskRules;
        if (timeFrame != null) TimeFrame = timeFrame;
        if (marketCondition != null) MarketCondition = marketCondition;
        if (isActive.HasValue) IsActive = isActive.Value;
        if (suggestedSlPercent.HasValue) SuggestedSlPercent = suggestedSlPercent;
        if (suggestedRrRatio.HasValue) SuggestedRrRatio = suggestedRrRatio;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
