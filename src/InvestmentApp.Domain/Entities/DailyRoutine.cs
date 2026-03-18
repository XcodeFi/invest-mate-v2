using MongoDB.Bson.Serialization.Attributes;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Entities;

public class DailyRoutine : AggregateRoot
{
    public string UserId { get; private set; } = null!;
    public DateTime Date { get; private set; }
    public string TemplateId { get; private set; } = null!;
    public string TemplateName { get; private set; } = null!;
    public List<RoutineItem> Items { get; private set; } = new();
    public int CompletedCount { get; private set; }
    public int TotalCount { get; private set; }
    public bool IsFullyCompleted { get; private set; }
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public DailyRoutine() { } // MongoDB

    public static DailyRoutine CreateFromTemplate(string userId, DateTime date,
        RoutineTemplate template, int currentStreak, int longestStreak)
    {
        var routine = new DailyRoutine
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId ?? throw new ArgumentNullException(nameof(userId)),
            Date = date.Date,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Items = template.Items.Select(t => new RoutineItem
            {
                Index = t.Index,
                Label = t.Label,
                Group = t.Group,
                Link = t.Link,
                IsRequired = t.IsRequired,
                IsCompleted = false,
                Emoji = t.Emoji
            }).ToList(),
            CompletedCount = 0,
            TotalCount = template.Items.Count,
            IsFullyCompleted = false,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return routine;
    }

    public void CompleteItem(int index)
    {
        var item = Items.FirstOrDefault(i => i.Index == index)
            ?? throw new ArgumentException($"Item index {index} not found.");
        if (item.IsCompleted) return;

        item.IsCompleted = true;
        item.CompletedAt = DateTime.UtcNow;
        RecalculateCounts();
    }

    public void UncompleteItem(int index)
    {
        var item = Items.FirstOrDefault(i => i.Index == index)
            ?? throw new ArgumentException($"Item index {index} not found.");
        if (!item.IsCompleted) return;

        item.IsCompleted = false;
        item.CompletedAt = null;
        RecalculateCounts();
    }

    public void ResetFromTemplate(RoutineTemplate template)
    {
        TemplateId = template.Id;
        TemplateName = template.Name;
        Items = template.Items.Select(t => new RoutineItem
        {
            Index = t.Index,
            Label = t.Label,
            Group = t.Group,
            Link = t.Link,
            IsRequired = t.IsRequired,
            IsCompleted = false,
            Emoji = t.Emoji
        }).ToList();
        CompletedCount = 0;
        TotalCount = template.Items.Count;
        IsFullyCompleted = false;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateStreak(int currentStreak, int longestStreak)
    {
        CurrentStreak = currentStreak;
        LongestStreak = longestStreak;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private void RecalculateCounts()
    {
        CompletedCount = Items.Count(i => i.IsCompleted);
        var allRequiredDone = Items.Where(i => i.IsRequired).All(i => i.IsCompleted);
        var wasCompleted = IsFullyCompleted;
        IsFullyCompleted = allRequiredDone && CompletedCount == TotalCount;

        if (IsFullyCompleted && !wasCompleted)
        {
            CompletedAt = DateTime.UtcNow;
            // Recalculate streak when fully completed
            CurrentStreak = CurrentStreak + 1;
            if (CurrentStreak > LongestStreak) LongestStreak = CurrentStreak;
        }
        else if (!IsFullyCompleted && wasCompleted)
        {
            CompletedAt = null;
            if (CurrentStreak > 0) CurrentStreak--;
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
