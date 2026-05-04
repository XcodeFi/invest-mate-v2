using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class JournalEntry : AggregateRoot
{
    // === Gắn kết linh hoạt ===
    public string UserId { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public string? PortfolioId { get; private set; }
    public string? TradeId { get; private set; }
    public string? TradePlanId { get; private set; }

    // === Loại entry ===
    public JournalEntryType EntryType { get; private set; }

    // === Nội dung ===
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public string? MarketContext { get; private set; }

    // === Cảm xúc & tâm lý ===
    public string? EmotionalState { get; private set; }
    public int? ConfidenceLevel { get; private set; }

    // === Snapshot giá tại thời điểm ghi ===
    public decimal? PriceAtTime { get; private set; }
    public decimal? VnIndexAtTime { get; private set; }
    public DateTime Timestamp { get; private set; }

    // === Meta ===
    public List<string> Tags { get; private set; } = new();
    public int? Rating { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public JournalEntry() { } // MongoDB

    public JournalEntry(
        string userId,
        string symbol,
        JournalEntryType entryType,
        string title,
        string content,
        string? portfolioId = null,
        string? tradeId = null,
        string? tradePlanId = null,
        string? emotionalState = null,
        int? confidenceLevel = null,
        decimal? priceAtTime = null,
        decimal? vnIndexAtTime = null,
        string? marketContext = null,
        List<string>? tags = null,
        DateTime? timestamp = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
        var trimmed = symbol.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        Symbol = trimmed.ToUpper();
        EntryType = entryType;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Content = content ?? string.Empty;
        PortfolioId = portfolioId;
        TradeId = tradeId;
        TradePlanId = tradePlanId;
        EmotionalState = emotionalState;
        ConfidenceLevel = confidenceLevel.HasValue ? Math.Clamp(confidenceLevel.Value, 1, 10) : null;
        PriceAtTime = priceAtTime;
        VnIndexAtTime = vnIndexAtTime;
        MarketContext = marketContext;
        Tags = tags ?? new List<string>();
        Rating = null;
        IsDeleted = false;
        Timestamp = timestamp ?? DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string? title = null,
        string? content = null,
        string? emotionalState = null,
        int? confidenceLevel = null,
        string? marketContext = null,
        List<string>? tags = null,
        JournalEntryType? entryType = null)
    {
        if (title != null) Title = title;
        if (content != null) Content = content;
        if (emotionalState != null) EmotionalState = emotionalState;
        if (confidenceLevel.HasValue) ConfidenceLevel = Math.Clamp(confidenceLevel.Value, 1, 10);
        if (marketContext != null) MarketContext = marketContext;
        if (tags != null) Tags = tags;
        if (entryType.HasValue) EntryType = entryType.Value;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetRating(int rating)
    {
        Rating = Math.Clamp(rating, 0, 5);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetPriceSnapshot(decimal? price, decimal? vnIndex)
    {
        PriceAtTime = price;
        VnIndexAtTime = vnIndex;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkTrade(string tradeId)
    {
        TradeId = tradeId;
        UpdatedAt = DateTime.UtcNow;
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

public enum JournalEntryType
{
    Observation,    // Theo dõi, ghi nhận
    PreTrade,       // Trước giao dịch
    DuringTrade,    // Trong khi nắm giữ
    PostTrade,      // Sau giao dịch
    Review,         // Tổng kết
    Decision        // Ép user ghi lý do giữ thay vì hành động (P4 Decision Engine v1.1)
}
