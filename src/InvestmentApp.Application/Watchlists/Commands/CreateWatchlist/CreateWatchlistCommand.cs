using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;

public class CreateWatchlistCommand : IRequest<WatchlistDto>
{
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Emoji { get; set; } = "⭐";
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public class CreateWatchlistCommandHandler : IRequestHandler<CreateWatchlistCommand, WatchlistDto>
{
    private readonly IWatchlistRepository _repo;

    public CreateWatchlistCommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<WatchlistDto> Handle(CreateWatchlistCommand request, CancellationToken ct)
    {
        var watchlist = new Watchlist(
            request.UserId,
            request.Name,
            request.Emoji,
            request.IsDefault,
            request.SortOrder
        );

        await _repo.AddAsync(watchlist, ct);

        return new WatchlistDto
        {
            Id = watchlist.Id,
            Name = watchlist.Name,
            Emoji = watchlist.Emoji,
            IsDefault = watchlist.IsDefault,
            SortOrder = watchlist.SortOrder,
            ItemCount = 0,
            CreatedAt = watchlist.CreatedAt,
            UpdatedAt = watchlist.UpdatedAt
        };
    }
}
