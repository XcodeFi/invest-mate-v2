using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;

public class CreateJournalEntryCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string Symbol { get; set; } = null!;
    public string EntryType { get; set; } = null!; // Observation, PreTrade, DuringTrade, PostTrade, Review
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? TradeId { get; set; }
    public string? TradePlanId { get; set; }
    public string? EmotionalState { get; set; }
    public int? ConfidenceLevel { get; set; }
    public decimal? PriceAtTime { get; set; }
    public string? MarketContext { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? Timestamp { get; set; }
}
