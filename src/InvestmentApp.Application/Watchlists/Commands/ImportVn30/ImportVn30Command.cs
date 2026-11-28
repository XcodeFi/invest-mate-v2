using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Domain.Constants;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.ImportVn30;

public class ImportVn30Command : IRequest<WatchlistDetailDto>
{
    public string UserId { get; set; } = null!;
    public string? WatchlistId { get; set; }
}

public class ImportVn30CommandHandler : IRequestHandler<ImportVn30Command, WatchlistDetailDto>
{
    private readonly IWatchlistRepository _repo;

    public ImportVn30CommandHandler(IWatchlistRepository repo)
    {
        _repo = repo;
    }

    public async Task<WatchlistDetailDto> Handle(ImportVn30Command request, CancellationToken ct)
    {
        Watchlist watchlist;

        if (!string.IsNullOrEmpty(request.WatchlistId))
        {
            watchlist = await _repo.GetByIdAsync(request.WatchlistId, ct)
                ?? throw new KeyNotFoundException($"Watchlist {request.WatchlistId} not found");

            if (watchlist.UserId != request.UserId)
                throw new UnauthorizedAccessException();
        }
        else
        {
            // Create new VN30 watchlist
            watchlist = new Watchlist(request.UserId, "VN30", "🏆", false, 10);
            await _repo.AddAsync(watchlist, ct);
        }

        watchlist.AddBulkItems(Vn30Basket.Symbols.ToArray());
        await _repo.UpdateAsync(watchlist, ct);

        return AddWatchlistItemCommandHandler.MapToDetail(watchlist);
    }
}
