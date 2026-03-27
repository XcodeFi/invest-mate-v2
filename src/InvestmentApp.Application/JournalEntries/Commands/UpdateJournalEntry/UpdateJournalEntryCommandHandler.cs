using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;

public class UpdateJournalEntryCommandHandler : IRequestHandler<UpdateJournalEntryCommand, bool>
{
    private readonly IJournalEntryRepository _journalEntryRepository;

    public UpdateJournalEntryCommandHandler(IJournalEntryRepository journalEntryRepository)
    {
        _journalEntryRepository = journalEntryRepository;
    }

    public async Task<bool> Handle(UpdateJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _journalEntryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (entry == null || entry.UserId != request.UserId)
            return false;

        JournalEntryType? entryType = null;
        if (!string.IsNullOrWhiteSpace(request.EntryType))
            entryType = Enum.Parse<JournalEntryType>(request.EntryType, ignoreCase: true);

        entry.Update(
            title: request.Title,
            content: request.Content,
            emotionalState: request.EmotionalState,
            confidenceLevel: request.ConfidenceLevel,
            marketContext: request.MarketContext,
            tags: request.Tags,
            entryType: entryType);

        if (request.Rating.HasValue)
            entry.SetRating(request.Rating.Value);

        await _journalEntryRepository.UpdateAsync(entry, cancellationToken);
        return true;
    }
}
