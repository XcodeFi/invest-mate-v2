namespace InvestmentApp.Domain.ValueObjects;

public class WatchlistItem
{
    public string Symbol { get; set; } = null!;
    public string? Note { get; set; }
    public decimal? TargetBuyPrice { get; set; }
    public decimal? TargetSellPrice { get; set; }
    public DateTime AddedAt { get; set; }
}
