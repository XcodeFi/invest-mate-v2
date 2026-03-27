using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;

public class DeleteJournalEntryCommand : IRequest<bool>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string Id { get; set; } = null!;
}

public class DeleteJournalEntryCommandHandler : IRequestHandler<DeleteJournalEntryCommand, bool>
{
    private readonly Application.Interfaces.IJournalEntryRepository _repository;

    public DeleteJournalEntryCommandHandler(Application.Interfaces.IJournalEntryRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entry == null || entry.UserId != request.UserId)
            return false;

        entry.SoftDelete();
        await _repository.UpdateAsync(entry, cancellationToken);
        return true;
    }
}
