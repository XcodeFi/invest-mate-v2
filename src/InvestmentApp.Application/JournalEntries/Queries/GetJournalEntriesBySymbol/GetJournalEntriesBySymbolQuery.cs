using MediatR;

namespace InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;

public class GetJournalEntriesBySymbolQuery : IRequest<List<JournalEntryDto>>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class JournalEntryDto
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? TradeId { get; set; }
    public string? TradePlanId { get; set; }
    public string EntryType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? MarketContext { get; set; }
    public string? EmotionalState { get; set; }
    public int? ConfidenceLevel { get; set; }
    public decimal? PriceAtTime { get; set; }
    public decimal? VnIndexAtTime { get; set; }
    public DateTime Timestamp { get; set; }
    public List<string> Tags { get; set; } = new();
    public int? Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetJournalEntriesBySymbolQueryHandler
    : IRequestHandler<GetJournalEntriesBySymbolQuery, List<JournalEntryDto>>
{
    private readonly Application.Interfaces.IJournalEntryRepository _repository;

    public GetJournalEntriesBySymbolQueryHandler(Application.Interfaces.IJournalEntryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<JournalEntryDto>> Handle(
        GetJournalEntriesBySymbolQuery request, CancellationToken cancellationToken)
    {
        var entries = await _repository.GetByUserIdAndSymbolAsync(
            request.UserId, request.Symbol, request.From, request.To, cancellationToken);

        return entries.Select(e => new JournalEntryDto
        {
            Id = e.Id,
            UserId = e.UserId,
            Symbol = e.Symbol,
            PortfolioId = e.PortfolioId,
            TradeId = e.TradeId,
            TradePlanId = e.TradePlanId,
            EntryType = e.EntryType.ToString(),
            Title = e.Title,
            Content = e.Content,
            MarketContext = e.MarketContext,
            EmotionalState = e.EmotionalState,
            ConfidenceLevel = e.ConfidenceLevel,
            PriceAtTime = e.PriceAtTime,
            VnIndexAtTime = e.VnIndexAtTime,
            Timestamp = e.Timestamp,
            Tags = e.Tags,
            Rating = e.Rating,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        }).ToList();
    }
}
