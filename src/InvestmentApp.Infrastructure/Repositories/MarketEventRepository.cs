using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class MarketEventRepository : IMarketEventRepository
{
    private readonly IMongoCollection<MarketEvent> _collection;

    public MarketEventRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MarketEvent>("market_events");

        var symbolDateIndex = Builders<MarketEvent>.IndexKeys.Combine(
            Builders<MarketEvent>.IndexKeys.Ascending(e => e.Symbol),
            Builders<MarketEvent>.IndexKeys.Ascending(e => e.EventDate));
        _collection.Indexes.CreateOne(new CreateIndexModel<MarketEvent>(symbolDateIndex));
    }

    public async Task<MarketEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => e.Id == id && !e.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => !e.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketEvent>> GetBySymbolAsync(
        string symbol, DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketEvent>.Filter.And(
            Builders<MarketEvent>.Filter.Eq(e => e.Symbol, symbol.ToUpper().Trim()),
            Builders<MarketEvent>.Filter.Eq(e => e.IsDeleted, false));

        if (from.HasValue)
            filter &= Builders<MarketEvent>.Filter.Gte(e => e.EventDate, from.Value);
        if (to.HasValue)
            filter &= Builders<MarketEvent>.Filter.Lte(e => e.EventDate, to.Value);

        return await _collection.Find(filter)
            .SortBy(e => e.EventDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MarketEvent entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(MarketEvent entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<MarketEvent>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(e => e.Id == id, update, cancellationToken: cancellationToken);
    }
}
