using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Alert rule entity - defines conditions for triggering alerts.
/// </summary>
public class AlertRule : AggregateRoot
{
    public string UserId { get; private set; }
    public string? PortfolioId { get; private set; }
    public string Name { get; private set; }
    public string AlertType { get; private set; }        // PriceAlert, DrawdownAlert, StopLossNear, etc.
    public string Condition { get; private set; }         // Above, Below, Exceeds
    public decimal Threshold { get; private set; }
    public string? Symbol { get; private set; }           // For price alerts
    public string Channel { get; private set; }           // InApp, Email
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? LastTriggeredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    private AlertRule() { } // MongoDB

    public AlertRule(string userId, string name, string alertType,
        string condition, decimal threshold, string channel,
        string? portfolioId = null, string? symbol = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AlertType = alertType ?? throw new ArgumentNullException(nameof(alertType));
        Condition = condition ?? "Exceeds";
        Threshold = threshold;
        Channel = channel ?? "InApp";
        PortfolioId = portfolioId;
        Symbol = symbol;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? name = null, string? alertType = null,
        string? condition = null, decimal? threshold = null,
        string? channel = null, bool? isActive = null,
        string? symbol = null, string? portfolioId = null)
    {
        if (name != null) Name = name;
        if (alertType != null) AlertType = alertType;
        if (condition != null) Condition = condition;
        if (threshold.HasValue) Threshold = threshold.Value;
        if (channel != null) Channel = channel;
        if (isActive.HasValue) IsActive = isActive.Value;
        if (symbol != null) Symbol = symbol;
        if (portfolioId != null) PortfolioId = portfolioId;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkTriggered()
    {
        LastTriggeredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
