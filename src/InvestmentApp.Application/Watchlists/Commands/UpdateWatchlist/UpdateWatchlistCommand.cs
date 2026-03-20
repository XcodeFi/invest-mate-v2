using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;

public class UpdateWatchlistCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Emoji { get; set; } = "⭐";
    public int SortOrder { get; set; }
}

public class UpdateWatchlistCommandHandler : IRequestHandler<UpdateWatchlistCommand, Unit>
{
    private readonly IWatchlistRepository _repo;

    public UpdateWatchlistCommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(UpdateWatchlistCommand request, CancellationToken ct)
    {
        var watchlist = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Watchlist {request.Id} not found");

        if (watchlist.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        watchlist.UpdateInfo(request.Name, request.Emoji, request.SortOrder);
        await _repo.UpdateAsync(watchlist, ct);

        return Unit.Value;
    }
}
