using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Journals.Queries.GetJournals;

public class GetJournalsQuery : IRequest<IEnumerable<JournalDto>>
{
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
}

public class JournalDto
{
    public string Id { get; set; } = null!;
    public string TradeId { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string EntryReason { get; set; } = string.Empty;
    public string MarketContext { get; set; } = string.Empty;
    public string TechnicalSetup { get; set; } = string.Empty;
    public string EmotionalState { get; set; } = string.Empty;
    public int ConfidenceLevel { get; set; }
    public string PostTradeReview { get; set; } = string.Empty;
    public string LessonsLearned { get; set; } = string.Empty;
    public int Rating { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? TradePlanId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetJournalsQueryHandler : IRequestHandler<GetJournalsQuery, IEnumerable<JournalDto>>
{
    private readonly ITradeJournalRepository _journalRepository;

    public GetJournalsQueryHandler(ITradeJournalRepository journalRepository)
    {
        _journalRepository = journalRepository;
    }

    public async Task<IEnumerable<JournalDto>> Handle(GetJournalsQuery request, CancellationToken cancellationToken)
    {
        var journals = string.IsNullOrEmpty(request.PortfolioId)
            ? await _journalRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            : await _journalRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);

        return journals.Select(j => new JournalDto
        {
            Id = j.Id,
            TradeId = j.TradeId,
            PortfolioId = j.PortfolioId,
            EntryReason = j.EntryReason,
            MarketContext = j.MarketContext,
            TechnicalSetup = j.TechnicalSetup,
            EmotionalState = j.EmotionalState,
            ConfidenceLevel = j.ConfidenceLevel,
            PostTradeReview = j.PostTradeReview,
            LessonsLearned = j.LessonsLearned,
            Rating = j.Rating,
            Tags = j.Tags ?? new(),
            TradePlanId = j.TradePlanId,
            CreatedAt = j.CreatedAt,
            UpdatedAt = j.UpdatedAt
        });
    }
}
