using MongoDB.Bson.Serialization.Attributes;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Entities;

public class RoutineTemplate
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = string.Empty;

    public string? UserId { get; set; } // null = built-in system template
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public bool IsOneTime { get; set; }
    public bool IsUrgent { get; set; }
    public List<RoutineItemTemplate> Items { get; set; } = new();
    public List<int>? AutoSuggestDaysOfWeek { get; set; } // 0=Sun..6=Sat
    public string? AutoSuggestMarketCondition { get; set; } // "crisis"
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [BsonConstructor]
    public RoutineTemplate() { }

    public RoutineTemplate(string? userId, string name, string emoji, string category,
        int estimatedMinutes, List<RoutineItemTemplate> items)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId;
        Name = name;
        Emoji = emoji;
        Category = category;
        EstimatedMinutes = estimatedMinutes;
        Items = items ?? new();
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? name = null, string? description = null, string? emoji = null,
        int? estimatedMinutes = null, List<RoutineItemTemplate>? items = null)
    {
        if (name != null) Name = name;
        if (description != null) Description = description;
        if (emoji != null) Emoji = emoji;
        if (estimatedMinutes.HasValue) EstimatedMinutes = estimatedMinutes.Value;
        if (items != null) Items = items;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
