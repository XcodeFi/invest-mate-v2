using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly IMongoCollection<Watchlist> _collection;

    public WatchlistRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Watchlist>("watchlists");

        var userIndex = Builders<Watchlist>.IndexKeys.Ascending(w => w.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Watchlist>(userIndex));
    }

    public async Task<Watchlist?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(w => w.Id == id && !w.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<Watchlist>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(w => !w.IsDeleted).SortBy(w => w.SortOrder).ToListAsync(ct);
    }

    public async Task AddAsync(Watchlist entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(Watchlist entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(w => w.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var update = Builders<Watchlist>.Update
            .Set(w => w.IsDeleted, true)
            .Set(w => w.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(w => w.Id == id, update, cancellationToken: ct);
    }

    public async Task<IEnumerable<Watchlist>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(w => w.UserId == userId && !w.IsDeleted)
            .SortBy(w => w.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<Watchlist?> GetDefaultByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(w => w.UserId == userId && w.IsDefault && !w.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }
}
