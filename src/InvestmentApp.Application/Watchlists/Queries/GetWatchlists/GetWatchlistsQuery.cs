using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Dtos;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Queries.GetWatchlists;

public class GetWatchlistsQuery : IRequest<List<WatchlistDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetWatchlistsQueryHandler : IRequestHandler<GetWatchlistsQuery, List<WatchlistDto>>
{
    private readonly IWatchlistRepository _repo;

    public GetWatchlistsQueryHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<WatchlistDto>> Handle(GetWatchlistsQuery request, CancellationToken ct)
    {
        var watchlists = await _repo.GetByUserIdAsync(request.UserId, ct);

        return watchlists.Select(w => new WatchlistDto
        {
            Id = w.Id,
            Name = w.Name,
            Emoji = w.Emoji,
            IsDefault = w.IsDefault,
            SortOrder = w.SortOrder,
            ItemCount = w.Items.Count,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList();
    }
}
