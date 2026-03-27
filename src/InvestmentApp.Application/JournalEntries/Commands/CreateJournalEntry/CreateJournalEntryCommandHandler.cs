using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;

public class CreateJournalEntryCommandHandler : IRequestHandler<CreateJournalEntryCommand, string>
{
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IAuditService _auditService;

    public CreateJournalEntryCommandHandler(
        IJournalEntryRepository journalEntryRepository,
        IMarketDataProvider marketDataProvider,
        IAuditService auditService)
    {
        _journalEntryRepository = journalEntryRepository;
        _marketDataProvider = marketDataProvider;
        _auditService = auditService;
    }

    public async Task<string> Handle(CreateJournalEntryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Symbol is required");

        var entryType = Enum.Parse<JournalEntryType>(request.EntryType, ignoreCase: true);

        // Auto-fill price if not provided
        decimal? priceAtTime = request.PriceAtTime;
        decimal? vnIndexAtTime = null;

        if (!priceAtTime.HasValue)
        {
            try
            {
                var priceData = await _marketDataProvider.GetCurrentPriceAsync(request.Symbol, cancellationToken);
                if (priceData != null)
                    priceAtTime = priceData.Close;
            }
            catch { /* Non-critical: proceed without price */ }
        }

        try
        {
            var indexData = await _marketDataProvider.GetIndexDataAsync("VNINDEX", cancellationToken);
            if (indexData != null)
                vnIndexAtTime = indexData.Close;
        }
        catch { /* Non-critical */ }

        var entry = new JournalEntry(
            userId: request.UserId!,
            symbol: request.Symbol,
            entryType: entryType,
            title: request.Title,
            content: request.Content,
            portfolioId: request.PortfolioId,
            tradeId: request.TradeId,
            tradePlanId: request.TradePlanId,
            emotionalState: request.EmotionalState,
            confidenceLevel: request.ConfidenceLevel,
            priceAtTime: priceAtTime,
            vnIndexAtTime: vnIndexAtTime,
            marketContext: request.MarketContext,
            tags: request.Tags,
            timestamp: request.Timestamp);

        await _journalEntryRepository.AddAsync(entry, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "CreatedJournalEntry",
            EntityId = entry.Id,
            EntityType = "JournalEntry",
            Description = $"{request.EntryType} entry for {request.Symbol}: {request.Title}"
        }, cancellationToken);

        return entry.Id;
    }
}
