using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class MarketIndexRepository : IMarketIndexRepository
{
    private readonly IMongoCollection<MarketIndex> _collection;

    public MarketIndexRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MarketIndex>("market_indices");

        var compoundIndex = Builders<MarketIndex>.IndexKeys.Combine(
            Builders<MarketIndex>.IndexKeys.Ascending(m => m.IndexSymbol),
            Builders<MarketIndex>.IndexKeys.Descending(m => m.Date)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<MarketIndex>(compoundIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<MarketIndex?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(m => m.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketIndex>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<MarketIndex?> GetBySymbolAndDateAsync(string indexSymbol, DateTime date, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketIndex>.Filter.And(
            Builders<MarketIndex>.Filter.Eq(m => m.IndexSymbol, indexSymbol.ToUpper()),
            Builders<MarketIndex>.Filter.Eq(m => m.Date, date.Date)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketIndex>> GetBySymbolAsync(string indexSymbol, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketIndex>.Filter.And(
            Builders<MarketIndex>.Filter.Eq(m => m.IndexSymbol, indexSymbol.ToUpper()),
            Builders<MarketIndex>.Filter.Gte(m => m.Date, from.Date),
            Builders<MarketIndex>.Filter.Lte(m => m.Date, to.Date)
        );
        return await _collection.Find(filter).SortBy(m => m.Date).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MarketIndex entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(MarketIndex entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(m => m.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(m => m.Id == id, cancellationToken);
    }

    public async Task UpsertAsync(MarketIndex marketIndex, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MarketIndex>.Filter.And(
            Builders<MarketIndex>.Filter.Eq(m => m.IndexSymbol, marketIndex.IndexSymbol),
            Builders<MarketIndex>.Filter.Eq(m => m.Date, marketIndex.Date)
        );

        var existing = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            existing.UpdateData(marketIndex.Open, marketIndex.High, marketIndex.Low, marketIndex.Close, marketIndex.Volume);
            await _collection.ReplaceOneAsync(filter, existing, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.InsertOneAsync(marketIndex, null, cancellationToken);
        }
    }
}
