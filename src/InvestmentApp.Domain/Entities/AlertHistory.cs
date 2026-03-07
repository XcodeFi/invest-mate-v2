using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Alert history entity - records triggered alert events.
/// </summary>
public class AlertHistory : AggregateRoot
{
    public string UserId { get; private set; }
    public string AlertRuleId { get; private set; }
    public string AlertType { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public string? PortfolioId { get; private set; }
    public string? Symbol { get; private set; }
    public decimal? CurrentValue { get; private set; }
    public decimal? ThresholdValue { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime TriggeredAt { get; private set; }

    [BsonConstructor]
    public AlertHistory() { } // MongoDB

    public AlertHistory(string userId, string alertRuleId, string alertType,
        string title, string message, string? portfolioId = null,
        string? symbol = null, decimal? currentValue = null,
        decimal? thresholdValue = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        AlertRuleId = alertRuleId ?? throw new ArgumentNullException(nameof(alertRuleId));
        AlertType = alertType ?? throw new ArgumentNullException(nameof(alertType));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Message = message ?? string.Empty;
        PortfolioId = portfolioId;
        Symbol = symbol;
        CurrentValue = currentValue;
        ThresholdValue = thresholdValue;
        IsRead = false;
        TriggeredAt = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
