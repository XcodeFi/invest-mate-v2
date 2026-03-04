using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Journals.Commands.DeleteJournal;

public class DeleteJournalCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteJournalCommandHandler : IRequestHandler<DeleteJournalCommand, Unit>
{
    private readonly ITradeJournalRepository _journalRepository;

    public DeleteJournalCommandHandler(ITradeJournalRepository journalRepository)
    {
        _journalRepository = journalRepository;
    }

    public async Task<Unit> Handle(DeleteJournalCommand request, CancellationToken cancellationToken)
    {
        var journal = await _journalRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Journal {request.Id} not found");

        if (journal.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to delete this journal");

        journal.SoftDelete();
        await _journalRepository.UpdateAsync(journal, cancellationToken);
        return Unit.Value;
    }
}
