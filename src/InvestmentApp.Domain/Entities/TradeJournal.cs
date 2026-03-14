using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Trade journal entity - captures pre/during/post trade analysis and emotions.
/// </summary>
public class TradeJournal : AggregateRoot
{
    public string TradeId { get; private set; }
    public string UserId { get; private set; }
    public string PortfolioId { get; private set; }
    public string? TradePlanId { get; private set; }

    // Pre-trade analysis
    public string EntryReason { get; private set; }
    public string MarketContext { get; private set; }
    public string TechnicalSetup { get; private set; }

    // During trade
    public string EmotionalState { get; private set; }
    public int ConfidenceLevel { get; private set; }  // 1-10

    // Post-trade review
    public string PostTradeReview { get; private set; }
    public string LessonsLearned { get; private set; }
    public int Rating { get; private set; }  // 1-5 stars

    public List<string> Tags { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public TradeJournal() { } // MongoDB

    public TradeJournal(string tradeId, string userId, string portfolioId,
        string entryReason, string marketContext, string technicalSetup,
        string emotionalState, int confidenceLevel)
    {
        Id = Guid.NewGuid().ToString();
        TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        EntryReason = entryReason ?? string.Empty;
        MarketContext = marketContext ?? string.Empty;
        TechnicalSetup = technicalSetup ?? string.Empty;
        EmotionalState = emotionalState ?? string.Empty;
        ConfidenceLevel = Math.Clamp(confidenceLevel, 1, 10);
        PostTradeReview = string.Empty;
        LessonsLearned = string.Empty;
        Rating = 0;
        Tags = new List<string>();
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? entryReason = null, string? marketContext = null,
        string? technicalSetup = null, string? emotionalState = null,
        int? confidenceLevel = null, string? postTradeReview = null,
        string? lessonsLearned = null, int? rating = null,
        List<string>? tags = null)
    {
        if (entryReason != null) EntryReason = entryReason;
        if (marketContext != null) MarketContext = marketContext;
        if (technicalSetup != null) TechnicalSetup = technicalSetup;
        if (emotionalState != null) EmotionalState = emotionalState;
        if (confidenceLevel.HasValue) ConfidenceLevel = Math.Clamp(confidenceLevel.Value, 1, 10);
        if (postTradeReview != null) PostTradeReview = postTradeReview;
        if (lessonsLearned != null) LessonsLearned = lessonsLearned;
        if (rating.HasValue) Rating = Math.Clamp(rating.Value, 0, 5);
        if (tags != null) Tags = tags;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void LinkTradePlan(string tradePlanId)
    {
        TradePlanId = tradePlanId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
