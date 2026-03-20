using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Dtos;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;

public class AddWatchlistItemCommand : IRequest<WatchlistDetailDto>
{
    public string WatchlistId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string? Note { get; set; }
    public decimal? TargetBuyPrice { get; set; }
    public decimal? TargetSellPrice { get; set; }
}

public class AddWatchlistItemCommandHandler : IRequestHandler<AddWatchlistItemCommand, WatchlistDetailDto>
{
    private readonly IWatchlistRepository _repo;

    public AddWatchlistItemCommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<WatchlistDetailDto> Handle(AddWatchlistItemCommand request, CancellationToken ct)
    {
        var watchlist = await _repo.GetByIdAsync(request.WatchlistId, ct)
            ?? throw new KeyNotFoundException($"Watchlist {request.WatchlistId} not found");

        if (watchlist.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        watchlist.AddItem(request.Symbol, request.Note, request.TargetBuyPrice, request.TargetSellPrice);
        await _repo.UpdateAsync(watchlist, ct);

        return MapToDetail(watchlist);
    }

    internal static WatchlistDetailDto MapToDetail(Domain.Entities.Watchlist w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Emoji = w.Emoji,
        IsDefault = w.IsDefault,
        SortOrder = w.SortOrder,
        Items = w.Items.Select(i => new WatchlistItemDto
        {
            Symbol = i.Symbol,
            Note = i.Note,
            TargetBuyPrice = i.TargetBuyPrice,
            TargetSellPrice = i.TargetSellPrice,
            AddedAt = i.AddedAt
        }).ToList(),
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };
}
