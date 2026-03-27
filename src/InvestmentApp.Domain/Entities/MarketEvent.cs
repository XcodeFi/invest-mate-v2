using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class MarketEvent : AggregateRoot
{
    public string Symbol { get; private set; } = null!;
    public MarketEventType EventType { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Source { get; private set; }
    public DateTime EventDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public MarketEvent() { } // MongoDB

    /// <summary>
    /// Market events are intentionally global (not user-scoped).
    /// Any authenticated user can create/view events — they serve as shared reference data
    /// for market events (earnings, dividends, news) visible to all traders on the platform.
    /// </summary>
    public MarketEvent(
        string symbol,
        MarketEventType eventType,
        string title,
        DateTime eventDate,
        string? description = null,
        string? source = null)
    {
        Id = Guid.NewGuid().ToString();
        ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
        var trimmed = symbol.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        Symbol = trimmed.ToUpper();
        EventType = eventType;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        EventDate = eventDate;
        Description = description;
        Source = source;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? title = null, string? description = null, string? source = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (source != null) Source = source;
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

public enum MarketEventType
{
    Earnings,        // KQKD
    Dividend,        // Cổ tức
    RightsIssue,     // Phát hành thêm
    ShareholderMtg,  // ĐHCĐ
    InsiderTrade,    // Giao dịch nội bộ
    News,            // Tin tức chung
    Macro            // Tin vĩ mô
}
