using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Journals.Commands.UpdateJournal;

public class UpdateJournalCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? EntryReason { get; set; }
    public string? MarketContext { get; set; }
    public string? TechnicalSetup { get; set; }
    public string? EmotionalState { get; set; }
    public int? ConfidenceLevel { get; set; }
    public string? PostTradeReview { get; set; }
    public string? LessonsLearned { get; set; }
    public int? Rating { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdateJournalCommandHandler : IRequestHandler<UpdateJournalCommand, Unit>
{
    private readonly ITradeJournalRepository _journalRepository;

    public UpdateJournalCommandHandler(ITradeJournalRepository journalRepository)
    {
        _journalRepository = journalRepository;
    }

    public async Task<Unit> Handle(UpdateJournalCommand request, CancellationToken cancellationToken)
    {
        var journal = await _journalRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Journal {request.Id} not found");

        if (journal.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this journal");

        journal.Update(
            request.EntryReason, request.MarketContext, request.TechnicalSetup,
            request.EmotionalState, request.ConfidenceLevel,
            request.PostTradeReview, request.LessonsLearned,
            request.Rating, request.Tags
        );

        await _journalRepository.UpdateAsync(journal, cancellationToken);
        return Unit.Value;
    }
}
