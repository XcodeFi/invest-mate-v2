using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;

namespace InvestmentApp.Application.Journals.Queries.GetJournalByTrade;

public class GetJournalByTradeQuery : IRequest<JournalDto?>
{
    public string TradeId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetJournalByTradeQueryHandler : IRequestHandler<GetJournalByTradeQuery, JournalDto?>
{
    private readonly ITradeJournalRepository _journalRepository;

    public GetJournalByTradeQueryHandler(ITradeJournalRepository journalRepository)
    {
        _journalRepository = journalRepository;
    }

    public async Task<JournalDto?> Handle(GetJournalByTradeQuery request, CancellationToken cancellationToken)
    {
        var journal = await _journalRepository.GetByTradeIdAsync(request.TradeId, cancellationToken);
        if (journal == null || journal.UserId != request.UserId) return null;

        return new JournalDto
        {
            Id = journal.Id,
            TradeId = journal.TradeId,
            PortfolioId = journal.PortfolioId,
            EntryReason = journal.EntryReason,
            MarketContext = journal.MarketContext,
            TechnicalSetup = journal.TechnicalSetup,
            EmotionalState = journal.EmotionalState,
            ConfidenceLevel = journal.ConfidenceLevel,
            PostTradeReview = journal.PostTradeReview,
            LessonsLearned = journal.LessonsLearned,
            Rating = journal.Rating,
            Tags = journal.Tags ?? new(),
            CreatedAt = journal.CreatedAt,
            UpdatedAt = journal.UpdatedAt
        };
    }
}
