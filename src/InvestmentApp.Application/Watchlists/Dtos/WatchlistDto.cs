namespace InvestmentApp.Application.Watchlists.Dtos;

public class WatchlistDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Emoji { get; set; } = "⭐";
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WatchlistDetailDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Emoji { get; set; } = "⭐";
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public List<WatchlistItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WatchlistItemDto
{
    public string Symbol { get; set; } = null!;
    public string? Note { get; set; }
    public decimal? TargetBuyPrice { get; set; }
    public decimal? TargetSellPrice { get; set; }
    public DateTime AddedAt { get; set; }
}
