using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;

public class RemoveWatchlistItemCommand : IRequest<WatchlistDetailDto>
{
    public string WatchlistId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
}

public class RemoveWatchlistItemCommandHandler : IRequestHandler<RemoveWatchlistItemCommand, WatchlistDetailDto>
{
    private readonly IWatchlistRepository _repo;

    public RemoveWatchlistItemCommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<WatchlistDetailDto> Handle(RemoveWatchlistItemCommand request, CancellationToken ct)
    {
        var watchlist = await _repo.GetByIdAsync(request.WatchlistId, ct)
            ?? throw new KeyNotFoundException($"Watchlist {request.WatchlistId} not found");

        if (watchlist.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        watchlist.RemoveItem(request.Symbol);
        await _repo.UpdateAsync(watchlist, ct);

        return AddWatchlistItemCommandHandler.MapToDetail(watchlist);
    }
}
