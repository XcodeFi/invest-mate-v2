using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;

public class DeleteWatchlistCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteWatchlistCommandHandler : IRequestHandler<DeleteWatchlistCommand, Unit>
{
    private readonly IWatchlistRepository _repo;

    public DeleteWatchlistCommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(DeleteWatchlistCommand request, CancellationToken ct)
    {
        var watchlist = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Watchlist {request.Id} not found");

        if (watchlist.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        await _repo.DeleteAsync(request.Id, ct);

        return Unit.Value;
    }
}
