using MongoDB.Bson.Serialization.Attributes;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Entities;

public class Watchlist : AggregateRoot
{
    public string UserId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Emoji { get; private set; } = "⭐";
    public bool IsDefault { get; private set; }
    public List<WatchlistItem> Items { get; private set; } = new();
    public int SortOrder { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public Watchlist() { } // MongoDB

    public Watchlist(string userId, string name, string emoji = "⭐", bool isDefault = false, int sortOrder = 0)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Emoji = emoji;
        IsDefault = isDefault;
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateInfo(string name, string emoji, int sortOrder)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Emoji = emoji;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void AddItem(string symbol, string? note = null, decimal? targetBuyPrice = null, decimal? targetSellPrice = null)
    {
        var normalized = symbol.ToUpper().Trim();
        if (Items.Any(i => i.Symbol == normalized))
            throw new InvalidOperationException($"Symbol {normalized} đã có trong watchlist");

        Items.Add(new WatchlistItem
        {
            Symbol = normalized,
            Note = note,
            TargetBuyPrice = targetBuyPrice,
            TargetSellPrice = targetSellPrice,
            AddedAt = DateTime.UtcNow
        });
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateItem(string symbol, string? note, decimal? targetBuyPrice, decimal? targetSellPrice)
    {
        var normalized = symbol.ToUpper().Trim();
        var item = Items.FirstOrDefault(i => i.Symbol == normalized)
            ?? throw new InvalidOperationException($"Symbol {normalized} không có trong watchlist");

        item.Note = note;
        item.TargetBuyPrice = targetBuyPrice;
        item.TargetSellPrice = targetSellPrice;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void RemoveItem(string symbol)
    {
        var normalized = symbol.ToUpper().Trim();
        var item = Items.FirstOrDefault(i => i.Symbol == normalized)
            ?? throw new InvalidOperationException($"Symbol {normalized} không có trong watchlist");

        Items.Remove(item);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void AddBulkItems(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            var normalized = symbol.ToUpper().Trim();
            if (!Items.Any(i => i.Symbol == normalized))
            {
                Items.Add(new WatchlistItem
                {
                    Symbol = normalized,
                    AddedAt = DateTime.UtcNow
                });
            }
        }
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
