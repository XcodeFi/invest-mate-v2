using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;

public class GetWatchlistDetailQuery : IRequest<WatchlistDetailDto>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetWatchlistDetailQueryHandler : IRequestHandler<GetWatchlistDetailQuery, WatchlistDetailDto>
{
    private readonly IWatchlistRepository _repo;

    public GetWatchlistDetailQueryHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<WatchlistDetailDto> Handle(GetWatchlistDetailQuery request, CancellationToken ct)
    {
        var watchlist = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Watchlist {request.Id} not found");

        if (watchlist.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        return AddWatchlistItemCommandHandler.MapToDetail(watchlist);
    }
}
