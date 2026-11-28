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
        var normalized = symbol.ToUpperInvariant().Trim();
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
        var normalized = symbol.ToUpperInvariant().Trim();
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
        var normalized = symbol.ToUpperInvariant().Trim();
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
            var normalized = symbol.ToUpperInvariant().Trim();
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

    /// <summary>
    /// Thêm nhiều mã, BỎ QUA mã đã có (không ném lỗi như <see cref="AddItem"/>), ghi chú chỉ gắn cho mã mới thêm. Mã trống bị bỏ qua hẳn.
    /// Version chỉ tăng (đúng một lần) khi thật sự có mã được thêm, để cập nhật có điều kiện theo Version không bị lệch vì lần gọi không làm gì.
    /// </summary>
    public (IReadOnlyList<string> Added, IReadOnlyList<string> Skipped) AddItemsIfMissing(IEnumerable<string> symbols, string? note)
    {
        var added = new List<string>();
        var skipped = new List<string>();
        foreach (var symbol in symbols)
        {
            var normalized = symbol?.ToUpperInvariant().Trim();
            if (string.IsNullOrEmpty(normalized)) continue;
            if (Items.Any(i => i.Symbol == normalized)) { skipped.Add(normalized); continue; }
            Items.Add(new WatchlistItem { Symbol = normalized, Note = note, AddedAt = DateTime.UtcNow });
            added.Add(normalized);
        }
        if (added.Count > 0)
        {
            UpdatedAt = DateTime.UtcNow;
            IncrementVersion();
        }
        return (added, skipped);
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
